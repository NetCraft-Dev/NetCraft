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

use std::ffi::c_void;
use std::path::PathBuf;
use std::sync::atomic::{AtomicBool, Ordering};

static STARTED: AtomicBool = AtomicBool::new(false);

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

    match stack::install() {
        Ok(()) => logging::write("stack overflow guard installed"),
        Err(message) => logging::write(&format!("stack overflow guard not installed: {message}")),
    }

    0
}

/// ncn_on_shutdown runs while the runtime is going down
#[no_mangle]
pub extern "C" fn ncn_on_shutdown() {
    logging::write("shutting down");
    stack::uninstall();
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
