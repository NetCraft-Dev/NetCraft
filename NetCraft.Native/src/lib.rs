//! NetCraft native layer
//!
//! The CLR loads this library as a profiler when the CORECLR_* environment variables are set, which happens before any
//! managed code in the process runs. Nothing injects it: the managed side writes the variables and starts the process
//! again, and the runtime picks the library up from there.
//!
//! Modules:
//! - `logging`: the single diagnostic file every part of the layer writes to
//! - `report`: the report produced once managed code can no longer run
//! - `stack`: the vectored exception handler that catches a stack overflow

mod logging;
mod report;
mod stack;

use std::ffi::{c_char, c_void, CStr};
use std::path::PathBuf;
use std::sync::atomic::{AtomicBool, AtomicUsize, Ordering};
use std::collections::HashSet;
use std::sync::{Mutex, OnceLock};

static STARTED: AtomicBool = AtomicBool::new(false);

//Set once the runtime accepts the compilation mask
static COMPILATIONS_REPORTED: AtomicBool = AtomicBool::new(false);

//Set once the managed side has handed over the overflow callback
//
//That call is made from the assembly holding the injected entry, so it doubles as the signal that this assembly is up
//and resolvable; nothing is injected before it arrives
static STACK_GUARD_READY: AtomicBool = AtomicBool::new(false);

//How many method bodies have been reported, only used for the first line of the log
static COMPILATION_COUNT: AtomicUsize = AtomicUsize::new(0);

//How many bodies were actually rewritten
static INJECTION_COUNT: AtomicUsize = AtomicUsize::new(0);

//Framework assemblies are left alone when no list is configured
//They carry their own stack probes already, and rewriting their bodies buys nothing while making every compile slower
//Anything else, the kernel and every mod alike, is fair game: a deep recursion can start anywhere
const FRAMEWORK_PREFIXES: &[&str] = &[
    "System",
    "Microsoft",
    "netstandard",
    "mscorlib",
    "WindowsBase",
    "PresentationCore",
];

//The assembly holding the entry the injected call points at, which must never be rewritten itself
const GUARD_ASSEMBLY: &str = "NetCraft.Util";

//The parsed list, read once on first use
//None means no list was configured, which stands for "everything except the framework"
static REWRITE_LIST: OnceLock<Option<Vec<String>>> = OnceLock::new();

//Module names already reported, so each one only lands in the log once
static SEEN_MODULES: OnceLock<Mutex<HashSet<String>>> = OnceLock::new();

//Whether any assembly has been accepted, and how many have been seen, used to notice a list that matches nothing
static ANY_ACCEPTED: AtomicBool = AtomicBool::new(false);
static SEEN_COUNT: AtomicUsize = AtomicUsize::new(0);

/// ncn_on_initialize runs on the profiling thread before any managed code
/// It is the earliest point this layer gets control, so everything is installed from here
#[no_mangle]
pub extern "C" fn ncn_on_initialize(_profiler_info_unknown: *mut c_void) -> i32 {
    if STARTED.swap(true, Ordering::SeqCst) {
        logging::write("initialize called twice, ignoring the repeat");
        return 0;
    }

    logging::write(&format!(
        "{} initializing, pid {}",
        stack::MODULE_NAME,
        std::process::id()
    ));

    // The handler is deliberately not installed here
    // Vectored handlers run newest first, and the runtime registers its own while it starts up, so a handler put in
    // place this early would sit behind it; waiting until the managed side asks for the callback puts this one first
    0
}

/// ncn_on_shutdown runs while the runtime is going down
#[no_mangle]
pub extern "C" fn ncn_on_shutdown() {
    logging::write("shutting down");
    stack::uninstall();
}

/// ncn_on_jit_notifications reports whether the runtime took the compilation mask
/// Without it no method is ever reported and the early stack check has nothing to attach to
#[no_mangle]
pub extern "C" fn ncn_on_jit_notifications(taken: bool) {
    COMPILATIONS_REPORTED.store(taken, Ordering::SeqCst);
    if taken {
        logging::write("the runtime took the compilation mask, early stack checks are possible");
    } else {
        logging::write("the runtime refused the compilation mask, early stack checks are not possible");
    }
}

/// ncn_on_jit_compiled runs for every method body the runtime is about to compile
/// It is called on runtime threads, so anything done here has to be quick and must never throw
#[no_mangle]
pub extern "C" fn ncn_on_jit_compiled(function_id: usize) -> i32 {
    let seen = COMPILATION_COUNT.fetch_add(1, Ordering::Relaxed);
    if seen == 0 {
        logging::write(&format!(
            "the first method body was reported for compilation, function id {function_id:#x}"
        ));
    }
    0
}

/// ncn_on_injected reports that the check was inserted into one method body
#[no_mangle]
pub extern "C" fn ncn_on_injected() {
    let seen = INJECTION_COUNT.fetch_add(1, Ordering::Relaxed);
    if seen == 0 {
        logging::write("the first method body carrying an early stack check was written");
    }
}

/// ncn_on_module_seen records one module the layer was asked about, logged once each
/// A run that shows a module missing here means its methods were never offered, which points at the notification
/// rather than the list; a module shown as refused points at the list
#[no_mangle]
pub unsafe extern "C" fn ncn_on_module_seen(simple_name: *const c_char, accepted: bool) {
    if simple_name.is_null() {
        return;
    }

    let Ok(name) = CStr::from_ptr(simple_name).to_str() else {
        return;
    };

    if name.is_empty() {
        return;
    }

    let seen = SEEN_MODULES.get_or_init(|| Mutex::new(HashSet::new()));
    let mut seen = match seen.lock() {
        Ok(guard) => guard,
        Err(_) => return,
    };

    if !seen.insert(name.to_string()) {
        return;
    }

    let verdict = if accepted { "accepted" } else { "refused" };
    logging::write(&format!("module {name}: {verdict}"));

    //A configured list that matches nothing looks exactly like a list that is simply narrow, and the whole mechanism
    //goes quietly dead. Once enough assemblies have gone by without a single match, that is worth saying out loud
    if accepted {
        ANY_ACCEPTED.store(true, Ordering::Relaxed);
        return;
    }

    let seen = SEEN_COUNT.fetch_add(1, Ordering::Relaxed) + 1;
    if seen == 10 && !ANY_ACCEPTED.load(Ordering::Relaxed) {
        logging::write(
            "warning: ten assemblies were seen and the rewrite list matched none of them; \
             check NCN_REWRITE_MODULES, a leftover value will disable the early stack check",
        );
    }
}

/// ncn_should_rewrite_module answers whether a module's methods get the early stack check
/// The list is read from NCN_REWRITE_MODULES, a comma or semicolon separated set of assembly simple names; when the
/// variable is absent a module only qualifies if its assembly references this ecosystem
/// `ecosystem_reference` carries that fact, which the shell reads off the module's metadata and the Rust side cannot see
#[no_mangle]
pub unsafe extern "C" fn ncn_should_rewrite_module(
    simple_name: *const c_char,
    ecosystem_reference: bool,
) -> i32 {
    if simple_name.is_null() {
        return 0;
    }

    let Ok(name) = CStr::from_ptr(simple_name).to_str() else {
        return 0;
    };

    if name.is_empty() {
        return 0;
    }

    if is_rewritable(name, ecosystem_reference) {
        1
    } else {
        0
    }
}

/// is_rewritable decides whether a module's methods get the early stack check
///
/// With NCN_REWRITE_MODULES set the list is taken as written and only what it names is rewritten, which is how a run
/// can be narrowed for measurement. Without it every assembly counts except the framework ones: a recursion that runs
/// away can begin in the kernel or in any mod, so leaving mods out would miss exactly the case this is meant to catch.
///
/// The default also requires the module to reference the ecosystem, because the injected call names a type in one of
/// the kernel's assemblies. A module that references none of them cannot bind that call and dies when its body is
/// compiled, which is what happens to the very process that starts the kernel: it is named with the same prefix while
/// only ever carrying its own assemblies
fn is_rewritable(name: &str, ecosystem_reference: bool) -> bool {
    let configured = REWRITE_LIST.get_or_init(|| {
        let resolved = std::env::var("NCN_REWRITE_MODULES")
            .ok()
            .map(|list| {
                list.split([',', ';'])
                    .map(|entry| entry.trim().to_string())
                    .filter(|entry| !entry.is_empty())
                    .collect::<Vec<String>>()
            })
            //A variable that is set but names nothing is treated the same as one that is absent: an empty list would
            //otherwise refuse every assembly, which silently turns the whole mechanism off
            .filter(|list| !list.is_empty());

        //The decision a run makes depends entirely on this value, so it is written down once when it is resolved
        //Without it a run that refuses everything looks the same as one whose list simply does not cover the target
        match &resolved {
            Some(list) => logging::write(&format!("rewrite list resolved to: {}", list.join(", "))),
            None => logging::write(
                "no rewrite list is set, every assembly except the framework ones that references this ecosystem is \
                 eligible",
            ),
        }

        resolved
    });

    match configured {
        Some(list) => list.iter().any(|entry| entry.eq_ignore_ascii_case(name)),
        None => !is_framework(name) && !is_guard_assembly(name) && ecosystem_reference,
    }
}

/// ncn_stack_guard_ready answers whether the assembly holding the injected entry has been loaded
/// The shell writes nothing before that: a body compiled earlier would demand the assembly ahead of the kernel's own
/// load order, and the main library would fail to load
#[no_mangle]
pub extern "C" fn ncn_stack_guard_ready() -> i32 {
    if STACK_GUARD_READY.load(Ordering::SeqCst) {
        1
    } else {
        0
    }
}

/// ncn_stack_guard answers whether the caller should raise the stack exception
/// The decision is made on the native side, where the thread's stack limits are readable
#[no_mangle]
pub extern "C" fn ncn_stack_guard() -> bool {
    unsafe { ncn_stack_guard_impl() != 0 }
}

extern "C" {
    fn ncn_stack_guard_impl() -> i32;
}

/// is_framework reports whether a simple name belongs to a framework assembly
fn is_framework(name: &str) -> bool {
    FRAMEWORK_PREFIXES
        .iter()
        .any(|prefix| name.starts_with(prefix))
}

/// is_guard_assembly reports whether a simple name holds the entry the injected call points at
/// Rewriting that assembly would put the check at the head of the check itself
fn is_guard_assembly(name: &str) -> bool {
    name.eq_ignore_ascii_case(GUARD_ASSEMBLY)
}

/// ncn_register_stack_overflow_callback takes the managed handler and the directory its reports belong in
/// The path arrives as UTF-8 bytes. Returns 0 when the callback was taken; without one the native report is still
/// written, so a failure here only costs the richer managed report
#[no_mangle]
pub unsafe extern "C" fn ncn_register_stack_overflow_callback(
    callback: usize,
    report_directory: *const u8,
    length: usize,
) -> i32 {
    //Reaching this entry means the guard assembly is loaded and can be resolved, so injection may begin from here
    //The moment is worth a line of its own: it is the boundary before which no method body is touched at all
    if !STACK_GUARD_READY.swap(true, Ordering::SeqCst) {
        logging::write("the guard assembly is loaded, early stack checks are enabled from here");
    }

    if callback == 0 || report_directory.is_null() || length == 0 {
        return -1;
    }

    let bytes = std::slice::from_raw_parts(report_directory, length);
    let directory = match std::str::from_utf8(bytes) {
        Ok(text) => PathBuf::from(text),
        Err(_) => return -2,
    };

    // The managed side hands over the address of a static method declared with the C calling convention
    let handler: stack::StackOverflowCallback = std::mem::transmute(callback);
    stack::register_callback(handler, directory);

    // Installed here rather than at profiler startup, for the ordering reason noted in ncn_on_initialize
    match stack::install() {
        Ok(()) => logging::write("stack overflow guard installed"),
        Err(message) => logging::write(&format!("stack overflow guard not installed: {message}")),
    }

    0
}

// The runtime looks these two up by name when it loads the library as a profiler
// They forward into the C++ shell, which is the only side that can build the COM objects the runtime asks for
extern "system" {
    fn ncn_com_get_class_object(rclsid: *const c_void, riid: *const c_void, ppv_object: *mut *mut c_void) -> i32;
    fn ncn_com_can_unload_now() -> i32;
}

#[no_mangle]
pub unsafe extern "system" fn DllGetClassObject(
    rclsid: *const c_void,
    riid: *const c_void,
    ppv_object: *mut *mut c_void,
) -> i32 {
    ncn_com_get_class_object(rclsid, riid, ppv_object)
}

#[no_mangle]
pub unsafe extern "system" fn DllCanUnloadNow() -> i32 {
    ncn_com_can_unload_now()
}
