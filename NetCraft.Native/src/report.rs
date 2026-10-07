//! The report written when the process is already past the point where managed code can run
//!
//! A stack overflow leaves nothing managed available, so this produces a short plain text file saying what happened
//! and where. It is deliberately brief: whatever stack Windows keeps in reserve has to cover writing it.

use crate::{logging, stack};
use std::fs::OpenOptions;
use std::io::Write;
use std::path::PathBuf;
use std::sync::OnceLock;
use std::time::{SystemTime, UNIX_EPOCH};

static REPORT_DIRECTORY: OnceLock<PathBuf> = OnceLock::new();

/// set_directory records where reports belong
/// The managed side calls this at startup so the report follows an output directory override
pub fn set_directory(directory: PathBuf) {
    let _ = REPORT_DIRECTORY.set(directory);
}

/// directory is the configured report location, or crash-reports next to the executable when none was set
fn directory() -> PathBuf {
    REPORT_DIRECTORY
        .get()
        .cloned()
        .or_else(|| logging::program_directory().map(|path| path.join("crash-reports")))
        .unwrap_or_else(|| PathBuf::from("crash-reports"))
}

/// write_stack_overflow drops the report and mirrors the reason into the diagnostic log
/// Only the pieces the handler managed to observe are recorded; anything richer needs a stack this thread no longer has
pub fn write_stack_overflow(reason: &str, thread_id: u32, address: usize) {
    let directory = directory();
    let _ = std::fs::create_dir_all(&directory);

    let stamp = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|elapsed| elapsed.as_secs())
        .unwrap_or(0);
    let path = directory.join(format!("native-crash-{stamp}.txt"));

    let text = format!(
        "---- NetCraft Native Crash Report ----\n\
         Time (unix): {stamp}\n\
         Description: A stack overflow ended this process\n\n\
         The CLR treats a stack overflow as unrecoverable: it terminates the process without running another line of\n\
         managed code, so a managed crash report for this run will not exist. This file is what the native layer was\n\
         able to record before handing control back.\n\n\
         -- Stack Overflow --\n\
         Details:\n\
         \tException: 0xC00000FD EXCEPTION_STACK_OVERFLOW\n\
         \tFaulting address: 0x{address:016X}\n\
         \tThread id: {thread_id}\n\
         \tReported by: {reporter}\n\
         \tHandled by: {module}\n\n\
         A detailed walkthrough needs a readable stack, which the thread that overflowed no longer has.\n\
         For the full picture run the process under a debugger or enable native dumps.\n",
        reporter = reason,
        module = stack::MODULE_NAME,
    );

    if let Ok(mut file) = OpenOptions::new().create(true).truncate(true).write(true).open(&path) {
        let _ = file.write_all(text.as_bytes());
        let _ = file.flush();
    }

    logging::write(&format!(
        "stack overflow ({reason}), report written to {}",
        path.display()
    ));
}
