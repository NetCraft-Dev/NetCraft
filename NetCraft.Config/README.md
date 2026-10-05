# NetCraft.Config

Global constants and switches. It maps to the original `net.minecraft.SharedConstants`, and every other module reads it at compile time.

## Features

- `SharedConstants` — immutable game metadata and format limits: version string, protocol version and its lower bound, world data version, ticks per second, chunk dimensions and region-file geometry, NBT string depth and byte limits, and the formatting prefix code.
- `Fixes` — behaviour switches for vanilla bug fixes, defaulting to match whether each fix is enabled in the reference implementation, so compatibility-sensitive dupes stay off and shipped fixes stay on.
- `OptimizationFlags` — compile-time performance toggles for the subsystems that have an optimized path: NBT I/O, registries, block states, networking, storage, commands, profiling, the rendering backend and chunk generation.
- `DebugFlags` — debug-only toggles and strict-validation switches, eliminated from release builds.
- `DebugMode` — the single runtime-mutable switch, set by the loader's `--debug` flag or by tests, gating verbose logging and extra debug behaviour.
