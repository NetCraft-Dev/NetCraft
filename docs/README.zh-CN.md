# NetCraft

> 本文件是仓库根 [README.md](../README.md) 的中文版，如中英文描述不一致，以英文版为准

**持续开发中。M1-M4 已达成，GPU submission/render phase 分离架构重构已完成，正推进 M5（启动+单机 tick）。文档与代码同步维护，各模块 `Overview.md` 反映当前状态。**

用 C# / .NET 10 从零重写 Minecraft 26.2 原版内核。

NetCraft 不是逐行翻译：各子系统按 C# 惯用法重写（`readonly struct`、`Span<T>`、源生成器、`AssemblyLoadContext`），同时保留原版运行时行为——NBT 字节序、注册表 id、区块序列化、网络包线路格式、DFU 升级路径均与原版字节级兼容。

## 项目状态

| | |
|---|---|
| 当前里程碑 | **M4 已达成**（端到端 TCP 握手），向 M5（启动序列 + 单机 tick）推进 |
| 整体完成度 | 约 65%（内核框架约 85%，游戏业务层约 62%，GPU 约 95%） |
| 代码规模 | 19 个内核子模块，约 1049 个 `.cs` 文件（GPU 子系统经 submission/render phase 重构后从 30 增至 ~120） |
| 测试用例 | 1163 条全过 0 失败 0 错误（gpugui 24/24 + guilogic 34/34 + 全套无回归） |
| 目标框架 | .NET 10 / C# 14，跨平台（不使用 `-windows` TFM，不使用 WinAPI） |
| 许可证 | GPL-3.0 |

## 核心特性

- **纯 C# 实现 DFU** —— 用 `K1`/`K2`/`App`/`Kind1` 三层结构模拟高阶类型，完整 Profunctor 光学体系，含 13 个 V1_21 Schema + 28 个 Fix。`v121fix` 端到端测试全绿。
- **NBT 100%** —— 13 种 Tag、大端字节序、GZIP 压缩、流式 + 完整访问者、`NbtOps` 桥接 Codec、SNBT 文法解析。24/24 round-trip 用例与 26.2 字节级兼容。
- **存档系统** —— MCA 区域文件、`SimpleBitStorage`、4 种 `PalettedContainer` 策略、三优先级抢占式 `IOWorker` 异步调度、`ChunkSource`/`ChunkHolder`/`ChunkMap` 异步管线替代同步 `GetChunk` 等待。
- **注册表** —— `Identifier` 值类型、per-T `ResourceKey<T>` intern 池、两阶段 `Direct`/`Reference` `Holder<T>` 绑定。
- **网络** —— `ClientConnection`/`ServerConnection` 状态机、`Varint`/`Varlong` 编解码、包压缩，M4 端到端握手已字节级验证通过。
- **GPU / GUI** —— 基于 Silk.NET 的 Vulkan 渲染器，采用对标原版 26.2 Blaze3D 的 **submission/render phase 分离架构**：submission 阶段（`GuiRenderContext`）构造不可变 `RenderState` 值对象 → `GuiRenderState`（node tree + strata）；render 阶段（`GuiRenderer`）按 (pipeline, texture, scissor) 排序合批，同组元素合并为 1 个 `DrawCall`。声明式 `Pipeline` + `Snippet` 组合 + `PipelineCache`（运行时零 shader 编译）。dynamic rendering 用 `VkPipelineRenderingCreateInfoKHR`。PIP 离屏 3D 渲染、blur 后处理、九宫格 sprite、动态图集、完整字体 providers 链、3D 物品渲染。Tick/Render 解耦（Tick 独立 20tps 线程，Render 仍串行在窗口循环）。详见 `minecraft/GPU模块重构技术规划.md` 与 `NetCraft.Gpu/Overview.md`。
- **命令** —— brigadier 完整移植（`LiteralArgumentBuilder`、`RequiredArgumentBuilder`、dispatcher、redirect、`ParsedCommandNode`），100%。
- **TPGA** —— 独立的认证/代理服务（Yggdrasil API 在 25565 + WSS/API 在 25566，自签证书兜底，ASP.NET Core 异步 I/O，SQLite 主/玩家库物理隔离）。与内核解耦。

## 仓库结构

层级即依赖分层。各模块详细说明见对应 `Overview.md`。

| Layer | 模块 | .cs | 完成度 | 职责 |
|---|---|---|---|---|
| 0 | `NetCraft.Primitives` | 8 | 85% | 值类型：`ChunkPos`、`BlockPos`、`SectionPos`、`Vec3i`、`Direction` 等 |
| 0 | `NetCraft.Config` | 4 | 100% | `SharedConstants`、`Fixes`、`Optimizations`、`DebugFlags` |
| 1 | `NetCraft.Util` | 82 | 80% | 日志、`CrashReport`、`BitSet`、`Mth`、线程调度、`Xoroshiro128++`、`Profiler` |
| 1 | `NetCraft.Nbt` | 28 | 100% | 13 种 Tag、`NbtOps`、SNBT 解析 |
| 1 | `NetCraft.Codec` | 17 | 70% | `Codec`/`MapCodec`/`DynamicOps`、`RecordCodecBuilder.Of2..Of4` |
| 1 | `NetCraft.Tags` | 4 | 70% | `TagLoader`、`TagManager`、`ITagLoader` 非泛型标记 |
| 1 | `NetCraft.DataFixer` | 166 | 95% | DFU 阶段 A–E、HKT 模拟、Profunctor 光学 |
| 2 | `NetCraft.Storage` | 55 | 90% | MCA、`PalettedContainer`、`IOWorker`、`ChunkSource` |
| 2 | `NetCraft.Registry` | 41 | 90% | `Identifier`、`ResourceKey<T>`、`Holder<T>` |
| 2 | `NetCraft.Interop` | 3 | 85% | 原生互操作 shim |
| 3 | `NetCraft.Network` | 56 | 80% | 连接状态机、编解码 |
| 3 | `NetCraft.Commands` | 49 | 100% | brigadier 移植 |
| 4 | `NetCraft.Resources` | 7 | 60% | 资源包框架 |
| 4 | `NetCraft.Gpu` | ~120 | ~95% | Vulkan + submission/render phase 分离架构（重构已完成，详见 `minecraft/GPU模块重构技术规划.md`） |
| 4 | `NetCraft.Optimizations` | 10 | 70% | 5/10 已集成（FerriteCore 风格 `FastMap` 等） |
| - | `NetCraft` | 8 | 90% | 内核入口，把所有子库 DLL 内嵌为资源 |
| - | `NetCraft.Bootstrap` | 1 | 75% | `BootstrapClass.bootStrap` |
| - | `NetCraft.Game` | 339 | 60% | 方块、实体、物品、区块生成、关卡、客户端/服务端 |
| - | `NetCraft.Test` | 46 | 100% | 测试宿主 |
| - | `NetCraft.Loader` | - | - | CLI 启动器、jar 资源提取 |
| - | `NetCraft.TPGA` | - | - | 认证/代理服务（独立） |
| - | `NetCraft.DataFixer.SourceGenerator` | - | - | DFU 的 Roslyn 源生成器 |

## 构建

需要 .NET 10 SDK。仓库使用 `.slnx` 解决方案与单一 `build.ps1` 编排脚本，后者同时把 `webui` React 前端构建到 `NetCraft.TPGA/wwwroot`。

```powershell
./build.ps1                 # webui + Debug
./build.ps1 Release         # webui + Release
./build.ps1 Rebuild         # 清理 + 重新构建（含 webui）
./build.ps1 Debug -SkipFrontend   # 仅构建 .NET，跳过 npm
```

## 文档导航

- 仓库根 [README.md](../README.md) —— 英文版项目入口
- 仓库根 [CHANGELOG.md](../CHANGELOG.md) —— 中英双语提交更新记录
- [mod-api.md](mod-api.md) —— 模组 API 参考
- [modding-guide.md](modding-guide.md) —— 怎么写一个模组
- [server-console.md](server-console.md) —— 无 GUI 启动时的服务端控制台
- 各模块根目录 `Overview.md` —— 模块定位、文件清单、状态、计划链位置

## 许可证

GPL-3.0 —— 见 [LICENSE](../LICENSE)。