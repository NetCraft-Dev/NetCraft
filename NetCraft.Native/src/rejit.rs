//! Runtime rewrite engine
//!
//! The managed side hands over a description of a method body; the request waits until the module it belongs to has
//! been loaded, and the runtime is then asked to recompile that method. When the recompile happens the runtime calls
//! back for the new body, which is where the description is turned into real metadata tokens and handed over.
//!
//! Nothing here may run on a managed thread: calling into the profiling API from one would re-enter the runtime, so
//! every request is fulfilled on a thread of this module's own.

use std::ffi::{c_char, c_void, CStr, CString};
use std::slice;
use std::sync::atomic::{AtomicU32, AtomicU64, Ordering};
use std::sync::{Condvar, Mutex, OnceLock};
use std::time::Duration;

use crate::il;
use crate::logging;

//A description carries its references as this sentinel until they are resolved; the real token replaces it afterwards
//The value names no metadata table, so it can never collide with a token the runtime produced itself
const REFERENCE_SENTINEL: u32 = 0x70FF_0000;

//Field references travel the same way but under their own sentinel, because the two are indexed in separate lists
const FIELD_SENTINEL: u32 = 0x71FF_0000;

//Type references carry a third sentinel for the same reason
const TYPE_SENTINEL: u32 = 0x72FF_0000;

//String literals carry a fourth
const STRING_SENTINEL: u32 = 0x73FF_0000;

//How long the fulfil thread waits before looking again, so a missed wake-up cannot stall a request forever
const WAIT_STEP: Duration = Duration::from_millis(200);

//The C++ shell owns every COM object and is the only side that can reach the profiling API
extern "C" {
    fn ncn_get_module_name(module_id: u64, buffer: *mut c_char, capacity: u32) -> i32;
    fn ncn_find_method(module_id: u64, type_name: *const c_char, method_name: *const c_char) -> u32;
    fn ncn_request_rejit(module_id: u64, method_def: u32) -> i32;
    fn ncn_set_il_function_body(function_control: *mut c_void, body: *const u8, size: u32) -> i32;
    fn ncn_get_il_function_body(module_id: u64, method_def: u32, buffer: *mut u8, capacity: u32) -> i32;
    fn ncn_initialize_current_thread() -> i32;
    fn ncn_define_user_string(module_id: u64, text: *const u16, length: u32) -> u32;
    fn ncn_ensure_type_ref(module_id: u64, assembly_name: *const c_char, type_name: *const c_char) -> u32;
    fn ncn_ensure_member_ref(
        module_id: u64,
        assembly_name: *const c_char,
        type_name: *const c_char,
        member_name: *const c_char,
        signature: *const u8,
        signature_size: u32,
    ) -> u32;
}

/// One method body the managed side wants replaced
/// The body travels as a description rather than as IL, because its references can only be resolved against the
/// module once that module is loaded
struct RewriteRequest {
    //The tail of the module file name, which is what identifies the target
    module_suffix: String,
    type_name: String,
    method_name: String,
    //The description the managed side produced
    description: Vec<u8>,
    //Written back on fulfilment so the callback can tell which request it has been asked about
    module_id: AtomicU64,
    token: AtomicU32,
}

//Every request handed over so far, together with the condition variable its thread waits on
static REQUESTS: OnceLock<(Mutex<Vec<RewriteRequest>>, Condvar)> = OnceLock::new();

//Loaded modules as (module id, file name), which is what requests are matched against
static MODULES: OnceLock<Mutex<Vec<(u64, String)>>> = OnceLock::new();

fn requests() -> &'static (Mutex<Vec<RewriteRequest>>, Condvar) {
    REQUESTS.get_or_init(|| (Mutex::new(Vec::new()), Condvar::new()))
}

fn modules() -> &'static Mutex<Vec<(u64, String)>> {
    MODULES.get_or_init(|| Mutex::new(Vec::new()))
}

/// wake nudges the fulfil thread after a new request or a new module
fn wake() {
    requests().1.notify_all();
}

/// fulfil_pending replaces every body whose module is loaded by now, and answers whether any are still waiting
fn fulfil_pending() -> bool {
    let loaded = modules().lock().unwrap();
    let (queue, _) = requests();
    let queue = queue.lock().unwrap();

    for request in queue.iter() {
        //One that already went through is left alone; asking twice is not something the runtime supports
        if request.token.load(Ordering::Relaxed) != 0 {
            continue;
        }
        for (module_id, name) in loaded.iter() {
            if name.ends_with(&request.module_suffix) {
                fulfil(request, *module_id);
                break;
            }
        }
    }

    queue
        .iter()
        .any(|request| request.token.load(Ordering::Relaxed) == 0)
}

/// start_worker brings up the thread that fulfils requests
///
/// A managed thread cannot reach the profiling API without re-entering the runtime, so every request lands here.
/// Registering the thread has to wait for the runtime to finish coming up, hence the retries.
pub(crate) fn start_worker() {
    static STARTED: OnceLock<()> = OnceLock::new();
    if STARTED.set(()).is_err() {
        return;
    }

    std::thread::spawn(|| {
        for _ in 0..40 {
            if unsafe { ncn_initialize_current_thread() } == 0 {
                break;
            }
            std::thread::sleep(Duration::from_millis(50));
        }

        let (queue, condvar) = requests();
        loop {
            fulfil_pending();

            let guard = queue.lock().unwrap();
            let _ = condvar.wait_timeout(guard, WAIT_STEP).unwrap();
        }
    });

    logging::write("the rewrite fulfil thread is up");
}

/// fulfil asks the runtime to recompile one method of a module that is loaded
/// The module id and the token are recorded on the request so the callback can match it later
fn fulfil(request: &RewriteRequest, module_id: u64) -> bool {
    let Ok(type_name) = CString::new(request.type_name.as_str()) else {
        return false;
    };
    let Ok(method_name) = CString::new(request.method_name.as_str()) else {
        return false;
    };

    let token = unsafe { ncn_find_method(module_id, type_name.as_ptr(), method_name.as_ptr()) };
    if token == 0 {
        logging::write(&format!(
            "{}::{} was not found in the module",
            request.type_name, request.method_name
        ));
        return false;
    }

    if unsafe { ncn_request_rejit(module_id, token) } != 0 {
        logging::write(&format!(
            "{}::{} could not be scheduled for recompilation",
            request.type_name, request.method_name
        ));
        return false;
    }

    request.module_id.store(module_id, Ordering::Relaxed);
    request.token.store(token, Ordering::Relaxed);
    logging::write(&format!(
        "{}::{} scheduled, token {token:#010X}",
        request.type_name, request.method_name
    ));
    true
}

/// ncn_on_module_load_finished records a module and wakes the fulfil thread
/// The fulfilment itself is not done here: this arrives on a runtime thread
#[no_mangle]
pub extern "C" fn ncn_on_module_load_finished(module_id: u64, hr_status: i32) {
    if hr_status != 0 {
        return;
    }

    let mut buffer = [0 as c_char; 512];
    if unsafe { ncn_get_module_name(module_id, buffer.as_mut_ptr(), buffer.len() as u32) } != 0 {
        return;
    }
    let name = unsafe { CStr::from_ptr(buffer.as_ptr()) }.to_string_lossy().into_owned();

    modules().lock().unwrap().push((module_id, name));
    wake();
}

/// ncn_on_module_unload_started drops a module that is going away
#[no_mangle]
pub extern "C" fn ncn_on_module_unload_started(module_id: u64) {
    modules().lock().unwrap().retain(|(id, _)| *id != module_id);
}

/// ncn_request_rewrite takes one rewrite request from the managed side
/// Only the new body is prepared by the caller; finding the method and asking for the recompile happen on the thread
/// started above, which is the only one allowed to reach the profiling API
#[no_mangle]
pub extern "C" fn ncn_request_rewrite(
    module_suffix: *const c_char,
    type_name: *const c_char,
    method_name: *const c_char,
    description: *const u8,
    size: u32,
) -> i32 {
    if module_suffix.is_null()
        || type_name.is_null()
        || method_name.is_null()
        || description.is_null()
        || size == 0
    {
        return -1;
    }

    let module_suffix = unsafe { CStr::from_ptr(module_suffix) }.to_string_lossy().into_owned();
    let type_name = unsafe { CStr::from_ptr(type_name) }.to_string_lossy().into_owned();
    let method_name = unsafe { CStr::from_ptr(method_name) }.to_string_lossy().into_owned();
    let description = unsafe { slice::from_raw_parts(description, size as usize) }.to_vec();

    logging::write(&format!(
        "rewrite requested for {module_suffix} {type_name}::{method_name}, {} bytes",
        description.len()
    ));

    requests().0.lock().unwrap().push(RewriteRequest {
        module_suffix,
        type_name,
        method_name,
        description,
        module_id: AtomicU64::new(0),
        token: AtomicU32::new(0),
    });
    wake();
    0
}

/// read_original reads a method's current IL and parses it, answering None when it cannot be read or understood
fn read_original(module_id: u64, method_def: u32) -> Option<il::MethodBody> {
    let mut raw = vec![0u8; 65536];
    let size =
        unsafe { ncn_get_il_function_body(module_id, method_def, raw.as_mut_ptr(), raw.len() as u32) };
    if size <= 0 {
        logging::write("the current body of the method could not be read");
        return None;
    }
    raw.truncate(size as usize);

    match il::MethodBody::parse(&raw) {
        Ok(parsed) => Some(parsed),
        Err(error) => {
            logging::write(&format!("the current body did not parse: {error}"));
            None
        }
    }
}

/// to_instructions turns described instructions into IL instructions
/// A branch target is carried as the index of the instruction it names, which is unique, and encoding matches on it
fn to_instructions(body: &il::wire::WireBody) -> Vec<il::Instruction> {
    body.instructions
        .iter()
        .enumerate()
        .map(|(index, instruction)| {
            let operand = match &instruction.operand {
                il::wire::WireOperand::None => il::Operand::None,
                il::wire::WireOperand::I32(value) => il::Operand::I32(*value),
                il::wire::WireOperand::I64(value) => il::Operand::I64(*value),
                il::wire::WireOperand::F32(value) => il::Operand::F32(*value),
                il::wire::WireOperand::F64(value) => il::Operand::F64(*value),
                il::wire::WireOperand::Var(value) => il::Operand::Var(*value),
                il::wire::WireOperand::Branch(target) => il::Operand::Branch(*target as u32),
                il::wire::WireOperand::Switch(targets) => {
                    il::Operand::Switch(targets.iter().map(|target| *target as u32).collect())
                }
                il::wire::WireOperand::Token(token) => il::Operand::Token(*token),
                //A reference is parked behind the sentinel and swapped for the real token once it has been resolved
                il::wire::WireOperand::Ref(reference) => {
                    il::Operand::Token(REFERENCE_SENTINEL | *reference as u32)
                }
                //Fields ride the same mechanism under their own sentinel, since the lists are separate
                il::wire::WireOperand::FieldRef(field) => {
                    il::Operand::Token(FIELD_SENTINEL | *field as u32)
                }
                //Types do as well: ldtoken names a type rather than a member
                il::wire::WireOperand::TypeRef(ty) => {
                    il::Operand::Token(TYPE_SENTINEL | *ty as u32)
                }
                //A string literal becomes a user string token, which is its own heap in the metadata
                il::wire::WireOperand::StringRef(text) => {
                    il::Operand::Token(STRING_SENTINEL | *text as u32)
                }
            };
            il::Instruction {
                offset: index as u32,
                code: instruction.code,
                operand,
            }
        })
        .collect()
}

/// build_body turns a description from the managed side into a body the runtime accepts
/// References are resolved into runtime tokens first, then the sentinels in the instructions are replaced
fn build_body(module_id: u64, method_def: u32, description: &[u8]) -> Result<Vec<u8>, String> {
    let described = il::wire::parse(description)?;

    let resolve_type = |assembly: &str, name: &str| -> Result<u32, String> {
        let assembly_name = CString::new(assembly).map_err(|_| "a null byte sits in an assembly name".to_string())?;
        let type_name = CString::new(name).map_err(|_| "a null byte sits in a type name".to_string())?;
        let token = unsafe { ncn_ensure_type_ref(module_id, assembly_name.as_ptr(), type_name.as_ptr()) };
        if token == 0 {
            Err(format!("no type reference could be made for {name}"))
        } else {
            Ok(token)
        }
    };

    let mut tokens = Vec::with_capacity(described.references.len());
    for reference in &described.references {
        let signature = il::wire::encode_signature(reference, &resolve_type)?;
        let assembly_name = CString::new(reference.assembly.as_str())
            .map_err(|_| "a null byte sits in an assembly name".to_string())?;
        let type_name = CString::new(reference.type_name.as_str())
            .map_err(|_| "a null byte sits in a type name".to_string())?;
        let member_name = CString::new(reference.member.as_str())
            .map_err(|_| "a null byte sits in a member name".to_string())?;

        let token = unsafe {
            ncn_ensure_member_ref(
                module_id,
                assembly_name.as_ptr(),
                type_name.as_ptr(),
                member_name.as_ptr(),
                signature.as_ptr(),
                signature.len() as u32,
            )
        };
        if token == 0 {
            return Err(format!(
                "no member reference could be made for {}::{}",
                reference.type_name, reference.member
            ));
        }

        logging::write(&format!(
            "reference {}::{} resolved to token {token:#010X}, signature {} bytes",
            reference.type_name,
            reference.member,
            signature.len()
        ));
        tokens.push(token);
    }

    let mut field_tokens = Vec::with_capacity(described.fields.len());
    for field in &described.fields {
        let signature = il::wire::encode_field_signature(field, &resolve_type)?;
        let assembly_name = CString::new(field.assembly.as_str())
            .map_err(|_| "a null byte sits in an assembly name".to_string())?;
        let type_name = CString::new(field.type_name.as_str())
            .map_err(|_| "a null byte sits in a type name".to_string())?;
        let field_name = CString::new(field.name.as_str())
            .map_err(|_| "a null byte sits in a field name".to_string())?;

        let token = unsafe {
            //A field goes through the same entry point as a method: the MemberRef table carries both and the
            //FIELD-tagged signature is what tells them apart
            ncn_ensure_member_ref(
                module_id,
                assembly_name.as_ptr(),
                type_name.as_ptr(),
                field_name.as_ptr(),
                signature.as_ptr(),
                signature.len() as u32,
            )
        };
        if token == 0 {
            return Err(format!(
                "no field reference could be made for {}::{}",
                field.type_name, field.name
            ));
        }

        logging::write(&format!(
            "field {}::{} resolved to token {token:#010X}",
            field.type_name, field.name
        ));
        field_tokens.push(token);
    }

    let mut type_tokens = Vec::with_capacity(described.types.len());
    for named in &described.types {
        let assembly_name = CString::new(named.assembly.as_str())
            .map_err(|_| "a null byte sits in an assembly name".to_string())?;
        let type_name = CString::new(named.name.as_str())
            .map_err(|_| "a null byte sits in a type name".to_string())?;

        let token = unsafe { ncn_ensure_type_ref(module_id, assembly_name.as_ptr(), type_name.as_ptr()) };
        if token == 0 {
            return Err(format!("no type reference could be made for {}", named.name));
        }

        logging::write(&format!(
            "type {} resolved to token {token:#010X}",
            named.name
        ));
        type_tokens.push(token);
    }

    let mut string_tokens = Vec::with_capacity(described.strings.len());
    for text in &described.strings {
        //The table holds UTF-16 bytes, which is exactly what the metadata heap wants
        let characters = (text.len() / 2) as u32;
        let token = unsafe { ncn_define_user_string(module_id, text.as_ptr() as *const u16, characters) };
        if token == 0 {
            return Err("a string literal could not be interned".to_string());
        }
        string_tokens.push(token);
    }

    let mut instructions = to_instructions(&described);
    for instruction in &mut instructions {
        let il::Operand::Token(value) = instruction.operand else {
            continue;
        };
        //Each sentinel names its own list, so which one it is decides where the index points
        let (list, index) = match value & 0xFFFF_0000 {
            REFERENCE_SENTINEL => (&tokens, value & 0xFFFF),
            FIELD_SENTINEL => (&field_tokens, value & 0xFFFF),
            TYPE_SENTINEL => (&type_tokens, value & 0xFFFF),
            STRING_SENTINEL => (&string_tokens, value & 0xFFFF),
            _ => continue,
        };
        let token = list
            .get(index as usize)
            .ok_or_else(|| format!("reference index {index} is out of range"))?;
        instruction.operand = il::Operand::Token(*token);
    }

    //A local variable signature is either carried over from the original body or left out entirely
    //Injecting a signature the original did not have is not supported yet
    let original = read_original(module_id, method_def);
    let carries_locals = described.flags & 1 != 0 && original.is_some();

    let local_var_sig_tok = match (&original, carries_locals) {
        (Some(body), true) => body.local_var_sig_tok,
        _ => 0,
    };
    let init_locals = carries_locals && original.as_ref().is_some_and(|body| body.init_locals);

    //The stack is given headroom on top of the original: a budget the managed side understates makes the body invalid
    let max_stack = described
        .max_stack
        .max(original.as_ref().map_or(0, |body| body.max_stack))
        .saturating_add(8);

    let body = il::MethodBody {
        max_stack,
        local_var_sig_tok,
        init_locals,
        instructions,
        was_fat: true,
    };
    Ok(body.encode())
}

/// ncn_on_get_rejit_parameters is where the runtime asks for the replacement IL
/// This is the moment the rewrite actually happens, and it runs only after the module is in place, which is exactly
/// why the references can be resolved here and nowhere earlier
#[no_mangle]
pub extern "C" fn ncn_on_get_rejit_parameters(
    module_id: u64,
    method_def: u32,
    function_control: *mut c_void,
) -> i32 {
    logging::write(&format!(
        "replacement IL requested, module {module_id} token {method_def:#010X}"
    ));

    //The request is claimed by the pair that was written back when it was fulfilled
    let requests = requests().0.lock().unwrap();
    let matched = requests.iter().find(|request| {
        request.token.load(Ordering::Relaxed) == method_def
            && request.module_id.load(Ordering::Relaxed) == module_id
    });

    let Some(request) = matched else {
        logging::write("no rewrite request matches, the original body is kept");
        return -1;
    };

    let built = match build_body(module_id, method_def, &request.description) {
        Ok(bytes) => bytes,
        Err(error) => {
            logging::write(&format!("the body could not be assembled: {error}, keeping the original"));
            return -1;
        }
    };

    let rc = unsafe { ncn_set_il_function_body(function_control, built.as_ptr(), built.len() as u32) };
    logging::write(&format!("{} bytes submitted, result {rc}", built.len()));
    rc
}
