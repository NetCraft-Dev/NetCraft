//Build the C++ COM shell and link it into the Rust cdylib
//The coreclr headers are vendored under third_party, so the build needs no external paths
fn main() {
    println!("cargo:rerun-if-changed=shell/com_base.cpp");
    println!("cargo:rerun-if-changed=shell/com_base.h");
    println!("cargo:rerun-if-changed=shell/entry.cpp");
    //Included by entry.cpp, and cargo does not track headers pulled in that way on its own
    println!("cargo:rerun-if-changed=third_party/coreclr/opcode.def");

    let windows = std::env::var("CARGO_CFG_TARGET_OS").as_deref() == Ok("windows");

    let mut build = cc::Build::new();
    build.cpp(true).std("c++20");
    //The vendored shell carries non-ASCII text in a few places; without this MSVC reads the source
    //in the local code page and mangles it. GCC and Clang already assume UTF-8, so the flag is optional there
    build.flag_if_supported("/utf-8");
    build.include("third_party/coreclr");

    //Two sets of headers: on Windows the SDK supplies unknwn.h and windows.h, everywhere else coreclr
    //ships its own copies under pal/ and pal/rt and those have to be on the include path instead
    if !windows {
        build.include("third_party/coreclr/rt");
        build.include("third_party/coreclr/pal");

        //The PAL headers pick architecture and OS definitions from HOST_* macros that coreclr's own
        //CMake normally supplies; missing one trips the "Unknown architecture" check in pal.h
        match std::env::var("CARGO_CFG_TARGET_ARCH").as_deref() {
            Ok("x86_64") => {
                build.define("HOST_AMD64", None);
                build.define("HOST_64BIT", None);
            }
            Ok("aarch64") => {
                build.define("HOST_ARM64", None);
                build.define("HOST_64BIT", None);
            }
            Ok("x86") => {
                build.define("HOST_X86", None);
            }
            Ok("arm") => {
                build.define("HOST_ARM", None);
            }
            _ => {}
        }
        build.define("HOST_UNIX", None);
        if std::env::var("CARGO_CFG_TARGET_OS").as_deref() == Ok("macos") {
            build.define("HOST_OSX", None);
        } else {
            build.define("HOST_LINUX", None);
        }
    }

    build
        .file("shell/com_base.cpp")
        .file("shell/entry.cpp")
        .compile("netcraft_native_shell");
}
