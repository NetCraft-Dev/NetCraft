//! Stack overflow handling
//!
//! The CLR treats a stack overflow as unrecoverable: it terminates the process without running another line of managed
//! code, so a managed crash report for that run can never happen on its own. The handler installed here is reached
//! through the vectored exception path, which runs ahead of the runtime's own handling, and the pages Windows keeps in
//! reserve are enough to write a report and, when the managed side registered one, to hand the overflow to a fresh
//! thread that still has a stack.

use crate::{logging, report};
use std::ffi::c_void;
use std::path::PathBuf;
use std::sync::atomic::{AtomicBool, AtomicPtr, Ordering};

/// Name recorded as the module that produced a report
pub const MODULE_NAME: &str = "NetCraft.Native";

/// EXCEPTION_STACK_OVERFLOW, raised when a thread touches its guard page
const EXCEPTION_STACK_OVERFLOW: u32 = 0xC000_00FD;

/// EXCEPTION_CONTINUE_SEARCH hands the exception back so the runtime can finish ending the process
/// The point of this handler is to leave a record first, not to take the overflow over
const EXCEPTION_CONTINUE_SEARCH: i32 = 0;

/// WAIT_OBJECT_0, the result of a wait that was satisfied
const WAIT_OBJECT_0: u32 = 0;

/// How long the overflowed thread waits for the managed callback before falling back to the native report
const CALLBACK_TIMEOUT_MILLIS: u32 = 10_000;

/// The signature the managed side registers, taking the faulting address as its only argument
pub type StackOverflowCallback = extern "C" fn(usize);

#[repr(C)]
pub struct ExceptionRecord {
    pub code: u32,
    pub flags: u32,
    pub record: *mut ExceptionRecord,
    pub address: *mut c_void,
    pub number_parameters: u32,
    pub information: [usize; 15],
}

#[repr(C)]
pub struct ExceptionPointers {
    pub record: *mut ExceptionRecord,
    pub context: *mut c_void,
}

type VectoredHandler = unsafe extern "system" fn(*mut ExceptionPointers) -> i32;

#[cfg(windows)]
extern "system" {
    fn AddVectoredExceptionHandler(first: u32, handler: VectoredHandler) -> *mut c_void;
    fn RemoveVectoredExceptionHandler(handle: *mut c_void) -> u32;
    fn CreateEventW(attributes: *mut c_void, manual_reset: i32, initial: i32, name: *const u16) -> *mut c_void;
    fn SetEvent(handle: *mut c_void) -> i32;
    fn WaitForSingleObject(handle: *mut c_void, milliseconds: u32) -> u32;
    fn CloseHandle(handle: *mut c_void) -> i32;
    fn GetCurrentThreadId() -> u32;
}

static HANDLER: AtomicPtr<c_void> = AtomicPtr::new(std::ptr::null_mut());
static CALLBACK: AtomicPtr<c_void> = AtomicPtr::new(std::ptr::null_mut());
static REPORTED: AtomicBool = AtomicBool::new(false);

/// install puts the vectored handler in front of the runtime's own exception handling
pub fn install() -> Result<(), String> {
    #[cfg(not(windows))]
    {
        Err("only the Windows vectored exception path is implemented".to_string())
    }

    #[cfg(windows)]
    {
        if !HANDLER.load(Ordering::SeqCst).is_null() {
            return Err("already installed".to_string());
        }

        // First in the chain, so this runs before the runtime gets a chance to end the process
        let handle = unsafe { AddVectoredExceptionHandler(1, handle_exception) };
        if handle.is_null() {
            return Err("AddVectoredExceptionHandler returned null".to_string());
        }

        HANDLER.store(handle, Ordering::SeqCst);
        Ok(())
    }
}

/// uninstall removes the handler again, used when the runtime is shutting down
pub fn uninstall() {
    #[cfg(windows)]
    {
        let handle = HANDLER.swap(std::ptr::null_mut(), Ordering::SeqCst);
        if !handle.is_null() {
            unsafe { RemoveVectoredExceptionHandler(handle) };
        }
    }
}

/// register_callback stores the managed handler and the directory its reports belong in
/// The callback runs on a fresh thread, because the thread that overflowed has nothing left to call anything on
pub fn register_callback(callback: StackOverflowCallback, directory: PathBuf) {
    report::set_directory(directory);
    CALLBACK.store(callback as *mut c_void, Ordering::SeqCst);
    logging::write("managed stack overflow callback registered");
}

#[cfg(windows)]
unsafe extern "system" fn handle_exception(exception: *mut ExceptionPointers) -> i32 {
    if exception.is_null() {
        return EXCEPTION_CONTINUE_SEARCH;
    }

    let record = (*exception).record;
    if record.is_null() || (*record).code != EXCEPTION_STACK_OVERFLOW {
        return EXCEPTION_CONTINUE_SEARCH;
    }

    // Only the first overflow is worth reporting; a second one means the reporting path ran out of stack too
    if REPORTED.swap(true, Ordering::SeqCst) {
        return EXCEPTION_CONTINUE_SEARCH;
    }

    notify_managed(record);
    EXCEPTION_CONTINUE_SEARCH
}

/// notify_managed hands the overflow to the managed side when one registered, and records the native report otherwise
#[cfg(windows)]
unsafe fn notify_managed(record: *mut ExceptionRecord) {
    let raw = CALLBACK.load(Ordering::SeqCst);
    let address = (*record).address as usize;
    let thread_id = GetCurrentThreadId();

    if raw.is_null() {
        report::write_stack_overflow("no managed callback was registered", thread_id, address);
        return;
    }

    let callback: StackOverflowCallback = std::mem::transmute(raw);

    // A manual-reset event is used instead of a runtime primitive: this code runs with almost no stack left, so it has
    // to avoid anything that might allocate or take a lock
    let done = CreateEventW(std::ptr::null_mut(), 1, 0, std::ptr::null());
    if done.is_null() {
        report::write_stack_overflow("the completion event could not be created", thread_id, address);
        return;
    }

    let completion = done as usize;
    std::thread::spawn(move || {
        callback(address);
        SetEvent(completion as *mut c_void);
    });

    let waited = WaitForSingleObject(done, CALLBACK_TIMEOUT_MILLIS);
    if waited != WAIT_OBJECT_0 {
        // The managed side may be holding a lock the overflowed thread never released, so a timeout is a real outcome
        report::write_stack_overflow("the managed callback did not finish in time", thread_id, address);
    }

    CloseHandle(done);
}
