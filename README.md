# NetCraft

A from-scratch reimplementation of the Minecraft 26.2 kernel in C# / .NET 10.

NetCraft is not a port. Nothing here is translated line by line from the original Java, and nothing is decompiled. Each subsystem is written against C# idioms — `readonly struct`, `Span<T>`, source generators, `AssemblyLoadContext` — while keeping observable behavior identical: the same NBT bytes, the same registry ids, the same chunk files, the same packet layouts, the same DFU upgrade paths.

## Where it stands

| | |
|---|---|
| Milestone | M1–M4 done. M4 verified a byte-level end-to-end TCP handshake. M5 (bootstrap + single-player tick) is in progress. |
| Scope | Kernel framework ~85%, game layer ~62%, GPU subsystem ~95% |
| Size | ~1.7k `.cs` files |
| Tests | 1163 cases, 0 failed |
| Runtime | .NET 10 / C# 14. Cross-platform — no `-windows` TFM, no WinAPI. |
| License | GPL-3.0 |

## What works

**Data formats**

- **NBT** — all 13 tag types, big-endian, GZIP, streaming readers plus the full visitor set, `NbtOps` bridging into `Codec`, and an SNBT grammar. 24/24 round-trip cases match 26.2 byte for byte.
- **DFU** — the data fixer upper in pure C#: higher-kinded simulation through `K1`/`K2`/`App`/`Kind1` and a Profunctor optics layer, 13 V1_21 schemas and 28 fixes. The `v121fix` end-to-end test is green.

**World**

- **Storage** — MCA region files, `SimpleBitStorage`, all four `PalettedContainer` strategies, and an `IOWorker` with a three-priority preemptive async scheduler behind the `ChunkSource` / `ChunkHolder` / `ChunkMap` pipeline.
- **Registry** — `Identifier` as a value type, a per-`T` `ResourceKey<T>` intern pool, two-phase `Direct` / `Reference` `Holder<T>` binding.

**Networking**

- **Network** — `ClientConnection` / `ServerConnection` state machines, `Varint` / `Varlong` codecs, packet compression. The M4 handshake was checked at the byte level.

**Rendering**

- **GPU / GUI** — a Vulkan renderer on Silk.NET that mirrors 26.2 Blaze3D's submission/render split. The submission phase builds immutable `RenderState` values into `GuiRenderState` (node tree + strata); the render phase sorts by pipeline, texture and scissor, then batches one `DrawCall` per group. Declarative `Pipeline` + `Snippet` composition backed by a `PipelineCache`, dynamic rendering via `VkPipelineRenderingCreateInfoKHR`, offscreen PIP 3D, blur post-processing, nine-slice sprites, a dynamic atlas, the full font provider chain, and 3D item rendering. Tick and render are decoupled — tick runs on its own 20 tps thread.

**Commands**

- **Brigadier port** — `LiteralArgumentBuilder`, `RequiredArgumentBuilder`, the dispatcher, redirects, `ParsedCommandNode`. Complete.

**Side services**

- **TPGA** — an auth/proxy service used alongside the kernel: Yggdrasil API on 25565, WSS/API on 25566, self-signed certificate fallback, ASP.NET Core async I/O, SQLite split into a master and a player database. It has no kernel dependency.

## Layout

Layers are dependency tiers — layer 0 depends on nothing, layer 4 sits on top of everything. Each module carries its own `Overview.md` with a file list and current status.

| Layer | Module | Files | Done | Role |
|---|---|---|---|---|
| 0 | `NetCraft.Primitives` | 30 | 85% | `ChunkPos`, `BlockPos`, `SectionPos`, `Vec3i`, `Direction`, voxel shapes |
| 0 | `NetCraft.Config` | 5 | 100% | `SharedConstants`, `Fixes`, `Optimizations`, `DebugFlags` |
| 1 | `NetCraft.Util` | 87 | 80% | Logging, `CrashReport`, `BitSet`, `Mth`, executors, `Xoroshiro128++`, `Profiler` |
| 1 | `NetCraft.Nbt` | 28 | 100% | 13 tags, `NbtOps`, SNBT parser |
| 1 | `NetCraft.Codec` | 18 | 70% | `Codec` / `MapCodec` / `DynamicOps`, `RecordCodecBuilder.Of2..Of4` |
| 1 | `NetCraft.Tags` | 4 | 70% | `TagLoader`, `TagManager` |
| 1 | `NetCraft.DataFixer` | 166 | 95% | DFU stages A–E, higher-kinded simulation, Profunctor optics |
| 2 | `NetCraft.Storage` | 110 | 90% | MCA, `PalettedContainer`, `IOWorker`, `ChunkSource` |
| 2 | `NetCraft.Registry` | 85 | 90% | `Identifier`, `ResourceKey<T>`, `Holder<T>` |
| 2 | `NetCraft.Interop` | 3 | 85% | Native interop shims |
| 3 | `NetCraft.Network` | 137 | 80% | Connection state machines, codecs |
| 3 | `NetCraft.Commands` | 50 | 100% | Brigadier port |
| 4 | `NetCraft.Resources` | 13 | 60% | Resource pack framework |
| 4 | `NetCraft.Gpu` | 132 | ~95% | Vulkan, submission/render phase separation |
| 4 | `NetCraft.Optimizations` | 11 | 70% | 5/10 integrations (FerriteCore-style `FastMap` and friends) |
| — | `NetCraft` | 13 | 90% | Kernel entry, embeds the sub-DLLs as resources |
| — | `NetCraft.Game` | 660 | 60% | Blocks, entities, items, chunk generation, levels, client/server |
| — | `NetCraft.Client` | 48 | — | Client runtime |
| — | `NetCraft.Server` | 30 | — | Dedicated server runtime |
| — | `NetCraft.ModLoader` | 17 | — | Runtime mod loading and injection |
| — | `NetCraft.Loader` | 1 | — | CLI launcher, jar asset extraction |
| — | `NetCraft.TPGA` | 43 | — | Auth/proxy service, independent of the kernel |
| — | `NetCraft.DataFixer.SourceGenerator` | 1 | — | Roslyn source generator for DFU |

## Build

Needs the .NET 10 SDK. The solution is `NetCraft.slnx`. `build.ps1` wraps the whole thing and also compiles the `webui` React frontend into `NetCraft.TPGA/wwwroot`.

```powershell
./build.ps1                      # webui + Debug
./build.ps1 Release              # webui + Release
./build.ps1 Rebuild              # clean, rebuild, webui included
./build.ps1 Debug -SkipFrontend  # .NET only, skip npm
```

## Repositories

| Repository | What it holds |
|---|---|
| **NetCraft** (this one) | Kernel, game layer, GPU, client/server, loader, TPGA |
| [NetCraft.ModApi](https://github.com/NetCraft-Dev/NetCraft.ModApi) | The mod API surface, together with the kernel reference assemblies it builds against |
| [NetCraft.DevTools](https://github.com/NetCraft-Dev/NetCraft.DevTools) | The `ncm` CLI and the `dotnet new ncm` project template |

Client, server and loader builds are published separately.

## Documentation

- `docs/modding-guide.md` — writing a mod
- `docs/mod-api.md` — ModApi reference
- `docs/server-console.md` — server console modes and commands
- `docs/README.zh-CN.md` — this README in Chinese
- `CHANGELOG.md` — bilingual changelog
- `NetCraft.*/Overview.md` — per-module status

## License

GPL-3.0. See [LICENSE](./LICENSE).
