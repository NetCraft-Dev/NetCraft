# NetCraft

A from-scratch reimplementation of Minecraft 26.2 in C# / .NET 10.

NetCraft is not a port. Nothing here is translated line by line from the original Java, and nothing is decompiled. Each subsystem is written against C# idioms — `readonly struct`, `Span<T>`, source generators, `AssemblyLoadContext` — while keeping observable behavior identical: the same NBT bytes, the same registry ids, the same chunk files, the same packet layouts, the same DFU upgrade paths.

## Capabilities

**Data formats**

- **NBT** — all 13 tag types, big-endian, GZIP, streaming readers plus the full visitor set, `NbtOps` bridging into `Codec`, and an SNBT grammar. Round-trips match 26.2 byte for byte.
- **DFU** — the data fixer upper in pure C#: higher-kinded simulation through `K1`/`K2`/`App`/`Kind1` and a Profunctor optics layer, the V1_21 schemas and their fixes.

**World**

- **Storage** — MCA region files, `SimpleBitStorage`, all four `PalettedContainer` strategies, and an `IOWorker` with a three-priority preemptive async scheduler behind the `ChunkSource` / `ChunkHolder` / `ChunkMap` pipeline. Chunk data, scheduled block and fluid ticks, lighting and saved data all round-trip through the same on-disk formats the original uses.
- **Registry** — `Identifier` as a value type, a per-`T` `ResourceKey<T>` intern pool, two-phase `Direct` / `Reference` `Holder<T>` binding.

**Networking**

- **Network** — `ClientConnection` / `ServerConnection` state machines, `Varint` / `Varlong` codecs, packet compression, and the full handshake, status, login, configuration and play protocol surface.

**Rendering**

- **GPU / GUI** — a Vulkan renderer on Silk.NET that mirrors 26.2 Blaze3D's submission/render split. The submission phase builds immutable `RenderState` values into `GuiRenderState` (node tree + strata); the render phase sorts by pipeline, texture and scissor, then batches one `DrawCall` per group. Declarative `Pipeline` + `Snippet` composition backed by a `PipelineCache`, dynamic rendering via `VkPipelineRenderingCreateInfoKHR`, offscreen PIP 3D, blur post-processing, nine-slice sprites, a dynamic atlas, the full font provider chain, and 3D item rendering. Tick and render are decoupled — tick runs on its own 20 tps thread.

**Commands**

- **Brigadier port** — `LiteralArgumentBuilder`, `RequiredArgumentBuilder`, the dispatcher, redirects, `ParsedCommandNode`.

**Modding**

- **ModLoader** — runtime mod loading with call-site, member and annotation driven injection rules, plus an embedded-assembly kernel that keeps sub-libraries out of the output directory.

**Side services**

- **TPGA** — an auth/proxy service used alongside the kernel: Yggdrasil API on 25565, WSS/API on 25566, self-signed certificate fallback, ASP.NET Core async I/O, SQLite split into a master and a player database. It has no kernel dependency.

## Layout

Layers are dependency tiers — layer 0 depends on nothing, layer 4 sits on top of everything. Each module carries its own `README.md` describing what it is responsible for.

| Layer | Module | Role |
|---|---|---|
| 0 | `NetCraft.Primitives` | `ChunkPos`, `BlockPos`, `SectionPos`, `Vec3i`, `Direction`, voxel shapes |
| 0 | `NetCraft.Config` | `SharedConstants`, `Fixes`, `Optimizations`, `DebugFlags` |
| 1 | `NetCraft.Util` | Logging, `CrashReport`, `BitSet`, `Mth`, executors, `Xoroshiro128++`, `Profiler` |
| 1 | `NetCraft.Nbt` | 13 tags, `NbtOps`, SNBT parser |
| 1 | `NetCraft.Codec` | `Codec` / `MapCodec` / `DynamicOps`, `RecordCodecBuilder` |
| 1 | `NetCraft.Tags` | `TagLoader`, `TagManager` |
| 1 | `NetCraft.DataFixer` | DFU stages, higher-kinded simulation, Profunctor optics |
| 2 | `NetCraft.Storage` | MCA, `PalettedContainer`, `IOWorker`, `ChunkSource`, scheduled ticks |
| 2 | `NetCraft.Registry` | `Identifier`, `ResourceKey<T>`, `Holder<T>` |
| 2 | `NetCraft.Interop` | Native interop shims |
| 3 | `NetCraft.Network` | Connection state machines, codecs |
| 3 | `NetCraft.Commands` | Brigadier port |
| 3 | `NetCraft.Network.Chat` | Text components |
| 4 | `NetCraft.Resources` | Resource pack framework |
| 4 | `NetCraft.Gpu` | Vulkan, submission/render phase separation |
| 4 | `NetCraft.Optimizations` | FerriteCore-style `FastMap` and friends |
| — | `NetCraft` | Kernel entry, embeds the sub-DLLs as resources |
| — | `NetCraft.Game` | Shared client/server gameplay code |
| — | `NetCraft.Client` | Client runtime and client-side protocol listeners |
| — | `NetCraft.Server` | Dedicated server runtime |
| — | `NetCraft.ModLoader` | Runtime mod loading and injection |
| — | `NetCraft.Loader` | CLI launcher, jar asset extraction |
| — | `NetCraft.TPGA` | Auth/proxy service, independent of the kernel |
| — | `NetCraft.DataFixer.SourceGenerator` | Roslyn source generator for DFU |


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
- `CHANGELOG.md` — release notes
- `NetCraft.*/README.md` — per-module description

## License

Apache-2.0. See [LICENSE](./LICENSE).
