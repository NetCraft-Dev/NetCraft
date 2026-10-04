# Changelog
更新记录

All notable changes to this project are documented in this file. Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), versioning follows [Semantic Versioning](https://semver.org/).

本项目所有重要变更记录在本文件中。格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

Currently in `0.1.x` pre-release: milestones M1–M4 reached, M5 in progress. Version is maintained in `VersionPrefix` of `Directory.Build.props`.

当前处于 `0.1.x` 预发布阶段：里程碑 M1–M4 已达成，M5 进行中。版本号在 `Directory.Build.props` 的 `VersionPrefix` 中维护。

---

## [Unreleased]

Pending changes not yet released. Next release will be `0.2.0`.

尚未发版的开发中变更。下一次发版将进入 `0.2.0`。

### Added
新增

#### Resource pipeline wiring
资源链路接通

- **`NetCraft.Resources/PreparableReloadListener.cs`**：Synchronous reload listener interface + context
  资源重载监听器接口 + `ReloadContext` 上下文，同步签名适配启动期阻塞模型
- **`NetCraft.Resources/SimpleReloadInstance.cs`**：Synchronous reload scheduler
  同步重载调度器按注册顺序遍历 listener 调 `Reload`
- **`NetCraft.Resources/PackMetadataSection.cs`**：pack.mcmeta metadata section parser
  pack.mcmeta 元数据段解析（`pack_format` + `description`），description 允许聊天组件对象返回 `GetRawText`
- **`NetCraft.Resources/ResourceManager`**：Reloaded event + Reload method
  新增 `Reloaded` 事件 + `Reload()` 方法 + `ReloadEventArgs`，供 AddPack/RemovePack 后显式触发 Tags 等数据驱动重载
- **`NetCraft.Bootstrap/TagsReloadListener.cs`**：Wraps LoadBuiltinTags + BindAll as a reload listener
  包装 `Bootstrap.LoadBuiltinTags` + `TagManager.BindAll`，实现 `PreparableReloadListener`，每次 Reload 清空旧 loader 重扫 tag 文件
- **`NetCraft.Game/ReloadableServerResources.cs`**：ReloadableServerResources minimal port
  服务端可重载资源集合精简版，持 `ResourceManager` + `TagManager` + listeners，`LoadResources` 静态工厂注册 TagsReloadListener 并触发首次重载
- **`NetCraft.Registry/BuiltInRegistries.CreateRegistryAccess()`**：Reflective RegistryAccess factory
  反射收集所有 `Registry<T>` 静态字段构造不可变 `ImmutableRegistryAccess`，供 `ReloadableServerResources.LoadResources` 调 `TagManager.BindAll` 使用
- **`NetCraft/AppPaths`** Added DataDir and DatapacksDir
  新增 `DataDir`（`data/` 子目录）与 `DatapacksDir`（`datapacks/` 子目录）
- **`NetCraft.Game/AssetsExtractor.TryExtractJar`** AssetsExtractor now extracts assets/ + data/ + root pack.mcmeta
  扩展同时提取 `assets/` 与 `data/` 前缀条目到各自目标目录，根 `pack.mcmeta` 复制到 `rootDir` 与 `assets`/`data` 同级，新增 `rootDir` 可选参数（null 时回退到 assetsDir 兼容旧行为）
- **`NetCraft.Game/ServerMain`** + **`NetCraft.Game/ClientMain`**：ServerMain and ClientMain wired to ResourceManager + LoadResources
  接入 `ResourceManager` + `ReloadableServerResources.LoadResources`：步骤 4.6 提取 jar 资源，步骤 4.7 构造 vanilla pack 并触发 Tags 数据驱动重载
- **`NetCraft.Game/Server/DedicatedServer`** + **`NetCraft.Game/Client/MinecraftClient`**：Optional rsr param on DedicatedServer and MinecraftClient
  新增可选 `ReloadableServerResources?` 构造参数与 `ServerResources` 属性，为后续 `/reload` 命令重载 Tags/Recipes/Advancements 等铺路
- **`NetCraft.Game/NetCraft.Game.csproj`**：Game now references Resources and Tags
  新增 `NetCraft.Resources` + `NetCraft.Tags` ProjectReference

#### GPU refactor
GPU 重构

> Major architectural change from immediate-mode to submission/render phase separation (mirrors vanilla 26.2 Blaze3D). Refactor plan stages 0-6+8 done, stage 7 partial (Tick on independent 20tps thread, Render still serial in `_window.Run`). GPU ~95% complete, file count grew from 30 to ~120.
>
> 重大架构变更，详见 `minecraft/GPU模块重构技术规划.md`（重构动机/目标架构/8 阶段计划/验收标准）与 `NetCraft/NetCraft.Gpu/Overview.md`。即时模式 → submission/render phase 分离架构（对标原版 26.2 Blaze3D）重构完成，规划阶段 0-6+8 已实现，阶段 7 部分完成（Tick 已独立 20tps 线程，Render 仍串行在 `_window.Run` 内）。GPU 子系统完成度 ~95%，文件数从 30 增至 ~120

- Decoupled from NetCraft.Logging via IGpuLogger; csproj drops NetCraft.Util/Interop refs
  **阶段 0 解耦准备**：新增 `IGpuLogger` 抽象 + `GpuLogger` 默认实现，`VulkanGuiApp` 构造注入解耦 NetCraft.Logging；`NetCraft.Gpu.csproj` 移除 NetCraft.Util/Interop ProjectReference，零 NetCraft.* 依赖
- New SPI-style ICommandEncoder/IRenderPass/PipelineCache abstractions, VulkanCommandEncoder/VulkanRenderPass implementations with dynamic rendering
  **阶段 1 GPU 抽象 SPI 改造**：新增 `ICommandEncoder.cs` / `IRenderPass.cs` / `PipelineCache.cs` 根目录接口，Vulkan 后端新增 `VulkanCommandEncoder.cs` / `VulkanRenderPass.cs`（dynamic rendering 用 `VkPipelineRenderingCreateInfoKHR` 无 VkRenderPass 对象，pipeline 双 handle withDepth/withoutDepth）
- 6 RenderState value objects + GuiRenderState container with node tree and strata
  **阶段 2 RenderState 数据层**：新增 `Render/RenderState/` 6 个值对象（`GuiElementRenderState`/`BlitRenderState`/`TiledBlitRenderState`/`ColoredRectangleRenderState`/`GlyphBlitRenderState`/`Pip/PictureInPictureRenderState`）+ `Render/GuiRenderState.cs`（node tree + strata + NextStratum/BlurBeforeThisStratum/Up/ForEachElement/SortElements/Reset）
- Full declarative pipeline system with Snippet composition and pipeline cache
  **阶段 3 Pipeline 声明式系统**：新增 `Pipeline/` 12 个文件全套（`RenderPipeline` record + `Snippet` 组合 + `PipelineBuilder` + `BindGroupLayout`/`DepthStencilState`/`ColorTargetState`/`BlendFunction`/`BlendEquation`/`PipelineEnums`/`ShaderDefines`/`ShaderManager`/`VertexFormat`），`PipelineCache` 接入 `IGpuDevice.PrecompilePipeline` 运行时零 shader 编译
- GuiRenderer with prepare/sort/blur-segmentation, StagedVertexBuffer, GpuBufferPool with fence-based recycling
  **阶段 4 GuiRenderer 执行层**：新增 `Render/GuiRenderer.cs`（Prepare 合批 + Draw 提交 + blur 分段 FirstMeshIndexAfterBlur + PreparePip）+ `Render/StagedVertexBuffer.cs` + `Render/GpuBufferPool.cs`（fence 异步回收）+ `Render/AutoStorageIndexBuffer.cs`（QUADS 自动索引）+ `Render/IVertexConsumer.cs`
- GuiRenderContext replaces VulkanGuiRenderer, VulkanGuiApp switches to new path
  **阶段 5 submission 层迁移**：新增 `Render/GuiRenderContext.cs` 替代旧 `VulkanGuiRenderer`，把控件 DrawQuad/DrawText/DrawImage 转换为 RenderState 提交到 GuiRenderState，`_recordingStack` 支持嵌套录制 + ReplayRange cache replay；`VulkanGuiApp` 切换到 GuiRenderContext+GuiRenderer+GuiResourceManager 新路径
- Retained mode with nested recording stack and dirty-flag skip
  **阶段 6 retained mode**：`GuiRenderContext._recordingStack` 子控件 cache 和 window cache 同时活跃，控件标记 dirty 未变子树跳过 submission
- Nine-slice sprite, dynamic atlas, PIP offscreen render, blur post-processing
  **阶段 8 高级特性**：新增 `Sprite/` 4 个（九宫格 `GuiSpriteScaling` Stretch/Tile/NineSlice + `.mcmeta` 解析）+ `Render/Atlas/` 7 个（`DynamicAtlasAllocator` + `GuiItemAtlas` + `ItemItemAtlas` 多 slot 共享 LoadOp=Load + `TextureStitcher` + `BlockTextureAtlas`）+ `Render/Pip/` 2 个（`PictureInPictureRenderer` + `ItemPipRenderer`，EnsureTexturesAndProjection 复用 texture 修复 descriptor pool 耗尽）+ `Shaders/core/blur.frag.glsl` + `VulkanGuiApp` blur 后处理（BeforeBlur 段 offscreen+水平+垂直 blur，AfterBlur 段 LoadOp=Load 保留模糊背景）
- Full font providers chain ported, F7 GlyphFont dynamic baking path
  **字体系统重构**：新增 `Font/` 19 个文件对标原版 `client.gui.font.providers` 链（`GlyphProvider`/`FontSet`/`SheetBakedGlyph`/`GlyphStitcher`/`TtfGlyphProvider`/`BitmapGlyphProvider`/`UnihexGlyphProvider` 等），F7 GlyphFont 动态烘焙路径替代 FontAtlas fallback
- 3D item rendering with CPU-side PoseStack transform
  **Item 3D 物品渲染**：新增 `Render/Item/` 5 个（`BakedQuad`/`CubeModel`/`VertexConsumer3D`/`ItemStackRenderState`/`ItemFeatureRenderer`），CPU 端 PoseStack transform 顶点 shader 端 Model=Identity
- Tick on independent 20tps thread via OnBeforeRun hook, Render still serial in _window.Run (remaining: Render thread independence)
  **阶段 7 Tick/Render 解耦（部分完成）**：`VulkanAppBase` 移除 FrameUpdate 事件订阅，`Run()` 只 `_window.Render += DrawFrame`，新增 `OnBeforeRun`/`OnAfterRun` 钩子供子类启动/Join Tick 线程；`VulkanGuiApp` 持有 `_latestSnapshot`（volatile）双缓冲 + `FrameTick` 事件由 Tick 线程 20tps 触发。Render 仍串行在 `_window.Run` 内（剩余优化是 Render 也独立线程）
- Full layout system ported from client.gui.layouts.*
  **GUI 布局系统扩展**：新增 `Gui/Layouts/` 11 个文件对标原版 `client.gui.layouts.*` 全套（`GridLayout`/`LinearLayout`/`FrameLayout`/`EqualSpacingLayout`/`HeaderAndFooterLayout`/`LayoutSettings`/`Divisor`/`SpacerElement` 等）

#### Tests
测试

- **`NetCraft.Test/Modules/ReloadableServerResourcesTests.cs`**：5 cases for ReloadableServerResources
  5 个用例覆盖 LoadResources 空 packs 不抛/TagsReloadListener 注册/Reload 重新触发/listeners 顺序/ReloadContext 序号
- **`NetCraft.Test/Modules/PackMetadataSectionTests.cs`**：7 cases for PackMetadataSection
  7 个用例覆盖 FromJson 标准/缺 description/缺 pack 节点/description 为对象/缺 pack_format/Reader 返回 null/Reader 从 StubPack 读取
- **`NetCraft.Test/Modules/AssetsExtractorTests.cs`**：5 cases for AssetsExtractor
  5 个用例覆盖提取 assets+data+pack.mcmeta/跳过 class+META-INF/dataDir null 跳过 data/rootDir null 回退/jar 不存在返回 false
- Test suite 814 → 831, no regression
  全测试套件 814 → 831 条，0 失败 0 错误无回归

### Changed
变更

- License changed from GPL-3.0 to Apache-2.0
  许可证由 GPL-3.0 改为 Apache-2.0，闭源模组不再受传染性条款约束
- Tags 70% → 90%, TagsReloadListener wired to startup
  `NetCraft.Tags` 完成度 70% → 90%，TagsReloadListener 已接入 ReloadableServerResources 启动序列
- Resources 60% → 85%, reload framework added
  `NetCraft.Resources` 完成度 60% → 85%，新增重载框架（PreparableReloadListener/SimpleReloadInstance/PackMetadataSection/Reloaded 事件）
- Game 60% → 62%, resource pipeline wired to startup
  `NetCraft.Game` 完成度 60% → 62%，启动流程接通资源链路
- Bootstrap 75% → 80%, TagsReloadListener added
  `NetCraft.Bootstrap` 完成度 75% → 80%，新增 TagsReloadListener.cs
- AssetsExtractor skips jar extraction and sound copy when target dir already non-empty
  `NetCraft.Game/AssetsExtractor.Extract` 检测目标目录已存在且非空则跳过 jar 提取与音频复制，避免每次启动重复解压

#### Docs
文档

- English project entry at repo root `README.md`
  仓库根 `README.md` 英文版项目入口
- Chinese project description at `docs/README.zh-CN.md`
  `docs/README.zh-CN.md` 中文版项目说明
- Bilingual commit changelog at repo root `CHANGELOG.md`
  仓库根 `CHANGELOG.md` 提交更新记录（中英双语）
- Module overviews updated
  `NetCraft.Resources/Overview.md`/`NetCraft.Tags/Overview.md`/`NetCraft.Game/Overview.md` 更新完成度、文件清单、接通状态、测试段
- Subsystem matrix updated
  `minecraft/子系统完成度与计划链.md` 矩阵更新 Resources/Tags/Game/Bootstrap 完成度与文件数

## [0.1.0] - 2026-08-03

First internal milestone release, covering M1–M4.

首个内部里程碑版本，覆盖 M1–M4。

### Added
新增

#### Kernel subsystems
内核子系统

- **`NetCraft.Primitives`**：8 value types with `asLong` bit-packed encoding
  8 个值类型（`ChunkPos`/`BlockPos`/`SectionPos`/`QuartPos`/`GlobalPos`/`Vec3i`/`Vec3`/`Direction`），含 `asLong` 位运算 pack
- **`NetCraft.Config`**：Compile-time `const` version flags and toggles
  `SharedConstants` 版本常量 + `Fixes`/`Optimizations`/`DebugFlags`，全部 `const` 编译期内联
- **`NetCraft.Util`**：Logging, crash reports, BitSet, math, executors, RNG, profiler
  日志、`CrashReport`、`BitSet`、`Mth`、线程调度（`IExecutor`/`ConsecutiveExecutor`/`PriorityConsecutiveExecutor`）、`Xoroshiro128++`、`Profiler`/`MetricsRecorder`
- **`NetCraft.Nbt`**：13 tag types, big-endian, GZIP, streaming + full visitors, SNBT parser (24/24 byte-compatible round-trip with 26.2)
  13 种 Tag、大端序、GZIP、流式 + 完整访问者、`NbtOps` 桥接 Codec、SNBT 文法解析（24/24 round-trip 字节级兼容 26.2）
- **`NetCraft.Codec`**：Codec framework with record builders
  `Codec`/`MapCodec`/`ScalarCodec`/`AbstractMapCodec`、`DataResult`/`Dynamic`/`OptionalDynamic`、`RecordCodecBuilder.Of2..Of4`
- **`NetCraft.DataFixer`**：Full DFU stages A–E with HKT simulation, Profunctor optics, 13 V1_21 schemas + 28 fixes (v121fix end-to-end green)
  DFU 阶段 A–E 全完成，HKT 模拟（`K1`/`K2`/`App`/`Kind1`）、Profunctor 光学体系、13 个 V1_21 Schema + 28 个 Fix（`v121fix` 端到端全绿）
- **`NetCraft.Storage`**：MCA, bit storage, 4 PalettedContainer strategies, async chunk pipeline, preemptive IOWorker
  MCA 读写层、`SimpleBitStorage`/`ZeroBitStorage`、4 种 `PalettedContainer` 策略、`SerializableChunkData`、`ChunkSource`/`ChunkHolder`/`ChunkMap` 异步调度、`IOWorker` 三优先级抢占
- **`NetCraft.Registry`**：Identifier value type, interned ResourceKey, two-phase Holder binding
  `Identifier` 值类型、`ResourceKey<T>` per-T intern 池、`Holder<T>` 两阶段 `Direct`/`Reference` 绑定
- **`NetCraft.Network`**：Connection state machines, varint codecs, packet compression
  `ClientConnection`/`ServerConnection` 状态机、`Varint`/`Varlong` 编解码、包压缩
- **`NetCraft.Commands`**：Full brigadier port (dispatcher, redirect, parsed nodes), 100%
  brigadier 完整移植（dispatcher、redirect、`ParsedCommandNode`），100%

#### GPU / GUI

- **`NetCraft.Gpu`**：Vulkan renderer on Silk.NET
  基于 Silk.NET 的 Vulkan 渲染器
- **M1 PoC**：Triangle + cube + GUI running on Vulkan
  三角形 + 立方体 + GUI 在 Vulkan 上跑通
- CPU Pose matrix stack + GPU dynamic Scissor, segment-grouped draws on Scissor change
  **P1 渲染上下文**：CPU Pose 矩阵栈（`System.Numerics.Matrix3x2`）+ GPU 动态 Scissor（`vkCmdSetScissor`），Scissor 变化时按段分组绘制
- IGuiLayout + GuiLinearLayout (horizontal/vertical stack, padding, spacing, skip invisible)
  **P2 布局引擎**：`IGuiLayout` 接口 + `GuiLinearLayout`（水平/垂直堆叠、`Padding`、`Spacing`，跳过不可见子控件）
- Inverted pipeline GUI_INVERT, RGB-inverting fragment shader for press feedback
  **P3 多 `RenderPipeline` 切换**：反色管线 `GUI_INVERT`，fragment shader 对 RGB 取反保留 alpha，用于按钮按下等视觉反馈
- Text measurement and alignment
  **P4 字体渲染增强**：`MeasureText`/`LineHeight` + `GuiTextAlign`（Left/Center/Right）
- TabStop/TabIndex + FocusNext for cyclic focus navigation across nested containers
  **P5 事件系统增强**：`TabStop`/`TabIndex` 属性 + `FocusNext` 方法实现 Tab 键焦点循环导航，深度遍历嵌套容器
- Removed business events from kernel GuiWindow, bridged via RawKeyDown to ScreenManager
  **内核 GUI 业务穿透清理**：移除 `GuiWindow` 的 `EscPressed`/`F3Pressed`/`HotbarSlotSelected` 业务事件，改由 `VulkanGuiApp.RawKeyDown` 桥接 `MinecraftClient` → `ScreenManager.HandleRawKeyDown` 处理业务键识别

#### Game layer
业务层

- **`NetCraft.Game`**：Blocks, entities, items, chunk gen, levels, dual-mode main loop, DedicatedServer tick wiring
  方块、实体、物品、区块生成（`ChunkGenerator` 抽象类 + `BiomeSource`）、关卡（`ServerLevel`/`SimpleServerLevel`）、`MinecraftServer`/`MinecraftClient` 双模式主循环（窗口驱动 + Headless）、`DedicatedServer.Tick` 调度链接入
- **`NetCraft.Bootstrap`**：Basic bootstrap, GameBootstrap noise settings injection
  `BootstrapClass.bootStrap` 基础完成，`GameBootstrap.Bootstrap()` 注入 `NoiseGeneratorSettings`
- **`NetCraft.Loader`**：CLI launcher, jar assets extraction, --debug global flag
  CLI 启动器，jar `assets` 目录解压到程序根 `assets`，`--debug` 开启全局调试
- **`NetCraft.TPGA`**：Standalone auth/proxy service
  独立认证/代理服务
  - Dual ports: 25565 (Yggdrasil) + 25566 (WSS/API)
    双端口：25565（Yggdrasil API）+ 25566（WSS/API）
  - WSS/HTTPS share cert, auto self-signed fallback
    WSS 与 HTTPS 共用证书，证书路径为空时自动生成自签证书
  - API auth required, WSS issues UUID after handshake
    API 请求需凭据认证，WSS 连接成功后生成 UUID 供后续 API 调用
  - SQLite master/player DB split
    SQLite 主库（管理员 + api 账户）与玩家库（账户 + 令牌 + 档案 + 加入记录）物理隔离
  - Boot order: config → i18n → log → db → cert+auth → ASP.NET
    启动编排顺序：配置 → I18n → 日志 → DB → 证书+验证 → ASP.NET
  - Multi-entry webui, build script handles `#` in path
    webui 前端多入口（player/admin），构建脚本 `webui/build-frontend.ps1` 处理 `#` 路径

#### Tests
测试

- **`NetCraft.Test`**：814 test cases
  814 个测试条目
  - 802 passing by default
    默认 802 条全过
  - M4 TCP handshake 2/2 (opt-in)
    M4 端到端 TCP 握手 2/2 全过（默认跳过，显式指定时运行）
  - GPU smoke tests 10 (opt-in)
    GPU 冒烟测试 10 条（默认跳过）

### Changed
变更

- Remote rebound to `XSY_xiaoqi`
  仓库远程地址重绑：`XSY-admin/NetCraft.git` → `XSY_xiaoqi/NetCraft.git`
- SSL verification disabled for self-signed cert server
  `git config http.sslVerify=false`（自签证书服务器）
- Nullable enabled, null-warning CS8597/8601/8602/8603/8604/8625 as errors
  `Directory.Build.props`：全局启用 `Nullable enable`、`WarningsAsErrors` 覆盖 `CS8597/8601/8602/8603/8604/8625`

### Fixed
修复

- 300-frame GUI render crash
  **GUI 300 帧渲染崩溃（0xC0000005）**
  - Cause: per-frame DeviceLocal vertex buffer upload caused GPU sync stalls and driver state accumulation
    原因：每帧 Upload `DeviceLocal` vertex buffer 导致 GPU 同步阻塞、驱动状态累积崩溃
  - Fix: HostVisible memory + map + memcpy, removing staging transfer and QueueSubmit sync
    修复：改用 `HostVisible` 内存 + `map` + `memcpy` 直接更新，消除 staging 中转与 `QueueSubmit` 同步开销
- Occasional Dispose AV
  **`VulkanGuiApp.Dispose` 偶发 AV（0xC0000005）**
  - Cause: InputContext release accessed destroyed GLFW window handle
    原因：`base.Dispose()` 销毁窗口后 `InputContext` 释放会访问已销毁的 GLFW 窗口句柄
  - Fix: dispose InputContext before base.Dispose
    修复：调整释放顺序为先 `_inputContext?.Dispose()` 再 `base.Dispose()`
- FocusNext nullable arg warning
  **`FocusNext` 可空实参警告（CS8604）**
  - Fix: Null-guard before IndexOf
    修复：`tabStops.IndexOf(_focusedControl)` → `_focusedControl is null ? -1 : tabStops.IndexOf(_focusedControl)`
- Missing Matrix3x2.CreateRotationZ
  **`Matrix3x2.CreateRotationZ` 不存在**
  - Fix: Use `CreateRotation` for 2D matrices
    修复：2D 矩阵使用 `Matrix3x2.CreateRotation` 替代

### Removed
移除

- `NetCraft.TPGA/wwwroot/` build output added to `.gitignore`
  `NetCraft.TPGA/wwwroot/` 构建产物目录纳入 `.gitignore`

## Early commits
早期提交

| Commit | Date | Description |
|---|---|---|
| `41f342c` | 2026-08-02 | Initial commit (repo init)<br>仓库初始化 |
| `0c87556` | 2026-08-03 | M1–M4 full code import<br>初始提交：NetCraft 内核重写项目（M1–M4 全量代码导入） |
| `753d906` | 2026-08-03 | Merge remote-tracking branch 'origin/main' |
| `cfc67bd` | 2026-08-03 | Ignore `NetCraft.TPGA/wwwroot` build output<br>忽略 `NetCraft.TPGA/wwwroot` 构建产物 |
| `83e55e3` | 2026-08-03 | GUI polish: P1 render context + P2 layout + P3 inverted pipeline + P4 text alignment + P5 Tab focus nav, fix 300-frame crash and Dispose AV<br>GUI 体系完善：P1 渲染上下文 + P2 布局引擎 + P3 反色管线 + P4 文本对齐 + P5 Tab 焦点导航，修复 300 帧崩溃与 Dispose AV |
