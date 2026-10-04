# NetCraft

用 C# / .NET 10 从零重写的 Minecraft 26.2 内核。

> 本文件是仓库根 [README.md](../README.md) 的中文版，如中英文描述不一致，以英文版为准。

NetCraft 不是移植。这里没有逐行翻译原版 Java，也没有反编译产物。各子系统按 C# 惯用法写（`readonly struct`、`Span<T>`、源生成器、`AssemblyLoadContext`），但对外的行为保持一致：同样的 NBT 字节、同样的注册表 id、同样的区块文件、同样的网络包布局、同样的 DFU 升级路径。

## 当前状态

| | |
|---|---|
| 里程碑 | M1–M4 已达成。M4 验证了字节级的端到端 TCP 握手。M5（启动序列 + 单机 tick）进行中。 |
| 完成度 | 内核框架 ~85%，业务层 ~62%，GPU 子系统 ~95% |
| 规模 | ~1.7k 个 `.cs` 文件 |
| 测试 | 1163 个用例，0 失败 |
| 运行时 | .NET 10 / C# 14。跨平台，不使用 `-windows` TFM，不调 WinAPI。 |
| 许可 | GPL-3.0 |

## 已经能跑通的部分

**数据格式**

- **NBT** —— 13 种 tag 全支持，大端序、GZIP、流式读取与完整的 visitor 体系，`NbtOps` 桥接到 `Codec`，另有 SNBT 语法。24/24 往返用例与 26.2 字节级一致。
- **DFU** —— 纯 C# 的数据修复器：用 `K1`/`K2`/`App`/`Kind1` 模拟高阶类型，配一套 Profunctor optics，13 个 V1_21 schema 与 28 条 fix。`v121fix` 端到端测试通过。

**世界**

- **存储** —— MCA 区域文件、`SimpleBitStorage`、`PalettedContainer` 的四种策略，以及 `ChunkSource` / `ChunkHolder` / `ChunkMap` 流水线背后的 `IOWorker`（三级优先级抢占式异步调度）。
- **注册表** —— `Identifier` 值类型、按 `T` 驻留的 `ResourceKey<T>` 池、两阶段的 `Direct` / `Reference` `Holder<T>` 绑定。

**网络**

- **网络** —— `ClientConnection` / `ServerConnection` 状态机、`Varint` / `Varlong` 编解码、包压缩。M4 握手在字节级核验过。

**渲染**

- **GPU / GUI** —— Silk.NET 上的 Vulkan 渲染器，对应 26.2 Blaze3D 的提交/渲染两阶段拆分。提交阶段把不可变的 `RenderState` 构造成 `GuiRenderState`（节点树 + 分层），渲染阶段按 pipeline、纹理、裁剪框排序，每组打一个 `DrawCall`。声明式 `Pipeline` + `Snippet` 组合配 `PipelineCache`，动态渲染走 `VkPipelineRenderingCreateInfoKHR`，另有离屏 PIP 三维、模糊后处理、九宫格贴图、动态图集、完整字体链路与三维物品渲染。Tick 与渲染解耦，tick 跑在自己的 20 tps 线程上。

**命令**

- **Brigadier 移植** —— `LiteralArgumentBuilder`、`RequiredArgumentBuilder`、dispatcher、redirect、`ParsedCommandNode`，已完整。

**周边服务**

- **TPGA** —— 与内核配套的认证/代理服务：25565 上跑 Yggdrasil API，25566 上跑 WSS/API，证书缺失时自签，ASP.NET Core 异步 I/O，SQLite 按主库与玩家库拆开。它不依赖内核。

## 仓库结构

分层即依赖层级 —— 第 0 层不依赖任何东西，第 4 层压在全部之上。每个模块自带一份 `Overview.md`，里面是文件清单与当前状态。

| 层 | 模块 | 文件数 | 完成度 | 职责 |
|---|---|---|---|---|
| 0 | `NetCraft.Primitives` | 30 | 85% | `ChunkPos`、`BlockPos`、`SectionPos`、`Vec3i`、`Direction`、体素形状 |
| 0 | `NetCraft.Config` | 5 | 100% | `SharedConstants`、`Fixes`、`Optimizations`、`DebugFlags` |
| 1 | `NetCraft.Util` | 87 | 80% | 日志、`CrashReport`、`BitSet`、`Mth`、执行器、`Xoroshiro128++`、`Profiler` |
| 1 | `NetCraft.Nbt` | 28 | 100% | 13 种 tag、`NbtOps`、SNBT 解析器 |
| 1 | `NetCraft.Codec` | 18 | 70% | `Codec` / `MapCodec` / `DynamicOps`、`RecordCodecBuilder.Of2..Of4` |
| 1 | `NetCraft.Tags` | 4 | 70% | `TagLoader`、`TagManager` |
| 1 | `NetCraft.DataFixer` | 166 | 95% | DFU 的 A–E 阶段、高阶类型模拟、Profunctor optics |
| 2 | `NetCraft.Storage` | 110 | 90% | MCA、`PalettedContainer`、`IOWorker`、`ChunkSource` |
| 2 | `NetCraft.Registry` | 85 | 90% | `Identifier`、`ResourceKey<T>`、`Holder<T>` |
| 2 | `NetCraft.Interop` | 3 | 85% | 原生互操作垫片 |
| 3 | `NetCraft.Network` | 137 | 80% | 连接状态机、编解码 |
| 3 | `NetCraft.Commands` | 50 | 100% | Brigadier 移植 |
| 4 | `NetCraft.Resources` | 13 | 60% | 资源包框架 |
| 4 | `NetCraft.Gpu` | 132 | ~95% | Vulkan、提交/渲染两阶段拆分 |
| 4 | `NetCraft.Optimizations` | 11 | 70% | 10 项集成完成 5 项（FerriteCore 风格的 `FastMap` 等） |
| — | `NetCraft` | 13 | 90% | 内核入口，把各子 DLL 作为资源内嵌 |
| — | `NetCraft.Game` | 660 | 60% | 方块、实体、物品、地形生成、维度、客户端/服务端 |
| — | `NetCraft.Client` | 48 | — | 客户端运行时 |
| — | `NetCraft.Server` | 30 | — | 服务端运行时 |
| — | `NetCraft.ModLoader` | 17 | — | 运行时模组加载与注入 |
| — | `NetCraft.Loader` | 1 | — | 命令行启动器、jar 资源提取 |
| — | `NetCraft.TPGA` | 43 | — | 认证/代理服务，与内核无关 |
| — | `NetCraft.DataFixer.SourceGenerator` | 1 | — | DFU 的 Roslyn 源生成器 |

## 构建

需要 .NET 10 SDK。解决方案是 `NetCraft.slnx`。`build.ps1` 包住整套流程，同时会把 `webui` 这个 React 前端编译进 `NetCraft.TPGA/wwwroot`。

```powershell
./build.ps1                      # webui + Debug
./build.ps1 Release              # webui + Release
./build.ps1 Rebuild              # 清理后重建，含 webui
./build.ps1 Debug -SkipFrontend  # 只编 .NET，跳过 npm
```

## 相关仓库

| 仓库 | 内容 |
|---|---|
| **NetCraft**（本仓库） | 内核、业务层、GPU、客户端/服务端、加载器、TPGA |
| [NetCraft.ModApi](https://github.com/NetCraft-Dev/NetCraft.ModApi) | 模组 API 表面，连同它编译所依赖的内核引用程序集 |
| [NetCraft.DevTools](https://github.com/NetCraft-Dev/NetCraft.DevTools) | `ncm` 命令行工具与 `dotnet new ncm` 项目模板 |

客户端、服务端与加载器的构建产物另行发布。

## 文档

- `docs/modding-guide.md` —— 怎么写一个模组
- `docs/mod-api.md` —— ModApi 参考
- `docs/server-console.md` —— 服务端控制台的两种模式与命令
- `docs/README.zh-CN.md` —— 本文件
- `CHANGELOG.md` —— 双语更新日志
- `NetCraft.*/Overview.md` —— 各模块状态

## 许可

GPL-3.0，见 [LICENSE](../LICENSE)。
