# NetCraft.Interop

The native interoperability helper layer. It has no direct counterpart in the original Java codebase; it exists because NetCraft needs shared, cross-platform access to native memory, dynamic libraries and memory-mapped files.

## Features

- Converts strings between managed values and native UTF-8 or UTF-16 buffers, including allocation and release of unmanaged memory. Buffers allocated by the runtime helper are owned by the caller and released explicitly.
- Loads and unloads native libraries, and resolves exported functions into delegates with platform-aware naming.
- Exposes platform detection and the derived library name, prefix and suffix conventions.
- Wraps memory-mapped files, with file-backed and anonymous mappings plus view streams, accessors and spans.
- Deliberately avoids platform-specific P/Invoke: all work goes through the .NET cross-platform standard APIs — `NativeMemory`, `NativeLibrary` and `MemoryMappedFile` — so the same code runs unchanged on Windows, Linux and macOS.
