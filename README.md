# NetCraft

**Active development. M1–M4 reached, GPU submission/render phase refactor done, pushing toward M5 (bootstrap + single-player tick). Documentation is kept in sync with the codebase; per-module `Overview.md` reflects current status.**

A from-scratch reimplementation of the Minecraft 26.2 vanilla kernel in C# / .NET 10.

NetCraft is not a port: subsystems are rewritten following C# idioms (readonly structs, `Span<T>`, source generators, `AssemblyLoadContext`) while preserving the original runtime behavior — NBT bytes, registry ids, chunk serialization, packet wire format, and DFU upgrade paths stay byte-compatible with vanilla.

## Status

| | |
|---|---|
| Milestone | **M4 passed** (end-to-end TCP handshake) — pushing toward M5 (bootstrap + single-player tick) |
| Overall completion | ~65% (kernel framework ~85%, game layer ~62%, GPU ~95%) |
| Codebase | ~1.7k `.cs` files across the kernel, game layer and GPU subsystem |
| Tests | 1163 cases all passing, 0 failed, 0 errored |
| Target framework | .NET 10 / C# 14, cross-platform (no `-windows` TFM, no WinAPI) |
| License | GPL-3.0 |

## Highlights

- **DFU in pure C#** — Higher-kind simulation via `K1`/`K2`/`App`/`Kind1` and a Profunctor optics system, including 13 V1_21 schemas and 28 fixes. `v121fix` end-to-end test green.
- **NBT 100%** — 13 tag types, big-endian, GZIP, streaming + full visitors, `NbtOps` bridge to Codec, SNBT grammar. 24/24 round-trip cases byte-compatible with 26.2.
- **Storage** — MCA region files, `SimpleBitStorage`, 4 `PalettedContainer` strategies, `IOWorker` three-priority preemptive async scheduler, `ChunkSource`/`ChunkHolder`/`ChunkMap` async pipeline replacing synchronous `GetChunk` waits.
- **Registry** — `Identifier` value type, per-T `ResourceKey<T>` intern pool, two-phase `Direct`/`Reference` `Holder<T>` binding.
- **Network** — `ClientConnection`/`ServerConnection` state machines, `Varint`/`Varlong` codecs, packet compression, M4 end-to-end handshake verified at the byte level.
- **GPU / GUI** — Vulkan renderer on Silk.NET with **submission/render phase separation** mirroring vanilla 26.2 Blaze3D: the submission phase (`GuiRenderContext`) constructs immutable `RenderState` value objects into `GuiRenderState` (node tree + strata); the render phase (`GuiRenderer`) sorts by (pipeline, texture, scissor) and batches to a single `DrawCall` per group. Declarative `Pipeline` + `Snippet` composition with `PipelineCache` (zero runtime shader compile), dynamic rendering via `VkPipelineRenderingCreateInfoKHR`, PIP offscreen 3D render, blur post-processing, nine-slice sprite, dynamic atlas, full font provider chain, 3D item rendering. Tick and render are decoupled (tick on an independent 20 tps thread, render still serial in the window loop). See `NetCraft.Gpu/Overview.md`.
- **Commands** — full brigadier port (`LiteralArgumentBuilder`, `RequiredArgumentBuilder`, dispatcher, redirect, `ParsedCommandNode`), 100%.
- **TPGA** — ancillary auth/proxy service (Yggdrasil API on 25565 + WSS/API on 25566, self-signed certificate fallback, ASP.NET Core async I/O, SQLite with master/player DB split). Independent of the kernel.

## Repository layout

Layers map directly to dependency tiers. Per-module details live in each `Overview.md`.

| Layer | Module | .cs | Completion | Role |
|---|---|---|---|---|
| 0 | `NetCraft.Primitives` | 30 | 85% | Value types: `ChunkPos`, `BlockPos`, `SectionPos`, `Vec3i`, `Direction` ... |
| 0 | `NetCraft.Config` | 5 | 100% | `SharedConstants`, `Fixes`, `Optimizations`, `DebugFlags` |
| 1 | `NetCraft.Util` | 87 | 80% | Logging, `CrashReport`, `BitSet`, `Mth`, executors, `Xoroshiro128++`, `Profiler` |
| 1 | `NetCraft.Nbt` | 28 | 100% | 13 tags, `NbtOps`, SNBT parser |
| 1 | `NetCraft.Codec` | 18 | 70% | `Codec`/`MapCodec`/`DynamicOps`, `RecordCodecBuilder.Of2..Of4` |
| 1 | `NetCraft.Tags` | 4 | 70% | `TagLoader`, `TagManager`, `ITagLoader` non-generic marker |
| 1 | `NetCraft.DataFixer` | 166 | 95% | DFU stages A–E, HKT simulation, Profunctor optics |
| 2 | `NetCraft.Storage` | 110 | 90% | MCA, `PalettedContainer`, `IOWorker`, `ChunkSource` |
| 2 | `NetCraft.Registry` | 85 | 90% | `Identifier`, `ResourceKey<T>`, `Holder<T>` |
| 2 | `NetCraft.Interop` | 3 | 85% | Native interop shims |
| 3 | `NetCraft.Network` | 137 | 80% | Connection state machines, codecs |
| 3 | `NetCraft.Commands` | 50 | 100% | brigadier port |
| 4 | `NetCraft.Resources` | 13 | 60% | Resource pack framework |
| 4 | `NetCraft.Gpu` | 132 | ~95% | Vulkan + submission/render phase separation |
| 4 | `NetCraft.Optimizations` | 11 | 70% | 5/10 integrations (FerriteCore-style `FastMap` etc.) |
| - | `NetCraft` | 13 | 90% | Kernel entry, embeds all sub-DLLs as resources |
| - | `NetCraft.Game` | 660 | 60% | Blocks, entities, items, chunk gen, level, client/server |
| - | `NetCraft.Client` | 48 | - | Client runtime |
| - | `NetCraft.Server` | 30 | - | Dedicated server runtime |
| - | `NetCraft.ModLoader` | 17 | - | Runtime mod loading and injection |
| - | `NetCraft.Loader` | 1 | - | CLI launcher, jar asset extraction |
| - | `NetCraft.TPGA` | 43 | - | Auth/proxy service (independent) |
| - | `NetCraft.DataFixer.SourceGenerator` | 1 | - | Roslyn source generator for DFU |

## Related repositories

| Repository | Contents |
|---|---|
| [NetCraft.ModApi](https://github.com/NetCraft-Dev/NetCraft.ModApi) | Mod API surface — source plus the kernel reference assemblies it builds against |
| [NetCraft.DevTools](https://github.com/NetCraft-Dev/NetCraft.DevTools) | `ncm` CLI and the mod project templates |

Client, server and loader builds are published separately.

## Build

Requires the .NET 10 SDK. The repo uses a `.slnx` solution and a single `build.ps1` orchestrator that also builds the `webui` React frontend into `NetCraft.TPGA/wwwroot`.

```powershell
./build.ps1                 # webui + Debug
./build.ps1 Release         # webui + Release
./build.ps1 Rebuild         # clean + rebuild (incl. webui)
./build.ps1 Debug -SkipFrontend   # .NET only, skip npm
```

## Documentation

- `docs/modding-guide.md` — how to write a NetCraft mod
- `docs/mod-api.md` — ModApi reference
- `docs/server-console.md` — server console modes and commands
- `docs/README.zh-CN.md` — Chinese version of this README
- `CHANGELOG.md` — bilingual (en/zh) release and commit changelog
- Per-module `Overview.md` — module purpose, file list, status, plan chain position

## License

GPL-3.0 — see [LICENSE](./LICENSE).
