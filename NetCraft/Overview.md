# NetCraft

> **完成度**：90% · **阶段**：M3 进行中（启动序列+注册表+存档 IO 联通）· **依赖**：内嵌全部 14 个子库 · **计划链位置**：顶层入口，聚合所有子库

主启动类库。聚合所有子库的公开 API，对应原版 `net.minecraft.server.Main` + `net.minecraft.Bootstrap` 入口职责。

注意：本类库不是 EXE，调用方需自行创建 Main 函数并调用 `NetCraftKernel.Initialize`。

## 实现

- 内嵌所有子库 DLL 为资源，运行时通过 `AssemblyLoadContext.Resolving` 回调按需加载
- 模块初始化器在程序集加载时自动注册资源解析，避免子库初始化时序问题
- `NetCraftKernel.Initialize` 唯一入口完成 全局引导+参数解析+banner
- `LaunchOptions` 事件驱动参数解析：内核识别的 flag/option 直接消费，未识别的通过 `UnhandledArgument` 事件广播给 Game 等业务模块订阅
- `Settings<T>` 泛型 properties 配置基类对齐原版 self type 模式，`ServerSettings`/`GameConfig` 子类暴露具体业务字段
- `PropertiesConfig` 通用 properties 文件读写容器支持 # 注释 key=value 行

## 文件清单（8 个 cs）

| 文件 | 用途 |
|------|------|
| `NetCraftKernel.cs` | 内核主入口 `Initialize` 完成 内嵌加载器初始化+日志源设置+banner 打印+参数解析，幂等 |
| `LaunchOptions.cs` | 启动参数事件驱动解析器，`DeclareKernelFlag`/`DeclareKernelOption` 供子模块声明识别的参数，`Parse` 消费内核识别的并通过 `UnhandledArgument` 事件发出未识别的 |
| `EmbeddedAssemblyLoader.cs` | 内嵌程序集加载器，从 `NetCraft.Embedded.*.dll` 资源加载字节流 |
| `ModuleInitialization.cs` | 模块初始化器，程序集加载时自动注册资源解析回调，早于 `Initialize` 调用解决"鸡生蛋" |
| `Settings.cs` | 泛型 properties 配置基类 `Settings<T>` 约束子类自引用，提供 Load/Save/Get/GetInt |
| `PropertiesConfig.cs` | properties 文件读写容器，支持 # 注释与 key=value 行 |
| `ServerSettings.cs` | 服务端 `server.properties` 配置继承 `Settings<ServerSettings>`，暴露 ServerPort/MaxPlayers 等字段 |
| `GameConfig.cs` | 客户端 `options.txt` 配置简化版，保留 RenderDistance/Fov/Gamma/Fullscreen/VSync 等核心字段 |

## 规划

- 阶段 11.46 已完成 ServerMain 接入 LevelStorage 初始化世界存储（原步骤 6 依赖）
- 阶段 11.46 已完成 MinecraftServer/MinecraftClient 主循环 tick 间隔控制（原步骤 7 依赖）
- 阶段 11.47 已完成 DedicatedServer.Tick 调度链接入（Connection/Level/Entity）
- 阶段 11.48 已完成 ChunkSource 异步调度子系统替代 PersistentServerLevel.GetChunk 同步等待
- 阶段 11.49 已完成 MinecraftClient 双模式主循环（窗口驱动 + Headless）+ Tick 四件套
- 阶段 11.52 已完成 ChunkGenerationHelper + DedicatedServer 可选 ChunkGenerator + MinecraftClient.Tick 接受 delta
- 主库基础入口已完成，后续业务接入由 Game 子库推进
- 后续：完整 M5 里程碑（Bootstrap.bootStrap 完整执行 + 单机生成新世界并 tick 一帧）
