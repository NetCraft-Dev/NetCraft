//! Diagnostics for the native layer
//!
//! A profiler has no console and no logging framework, so everything goes into one file next to the program.
//! Every write is best effort: failing to log must never be what takes the process down.

use std::fs::OpenOptions;
use std::io::Write;
use std::path::PathBuf;
use std::sync::OnceLock;
use std::time::{SystemTime, UNIX_EPOCH};

static LOG_PATH: OnceLock<PathBuf> = OnceLock::new();

/// write appends one line with a unix timestamp to the layer's log
pub fn write(message: &str) {
    let path = LOG_PATH.get_or_init(resolve_path);
    let seconds = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|elapsed| elapsed.as_secs())
        .unwrap_or(0);

    if let Ok(mut file) = OpenOptions::new().create(true).append(true).open(path) {
        let _ = writeln!(file, "[{seconds}] {message}");
    }
}

/// resolve_path picks where the log lives
/// The directory holding the executable comes first; when that is not writable the temporary directory is used instead
fn resolve_path() -> PathBuf {
    if let Some(directory) = program_directory() {
        let candidate = directory.join("netcraft_native.log");
        if OpenOptions::new().create(true).append(true).open(&candidate).is_ok() {
            return candidate;
        }
    }
    std::env::temp_dir().join("netcraft_native.log")
}

/// program_directory is the folder the executable runs from
pub fn program_directory() -> Option<PathBuf> {
    std::env::current_exe().ok().and_then(|path| path.parent().map(PathBuf::from))
}
