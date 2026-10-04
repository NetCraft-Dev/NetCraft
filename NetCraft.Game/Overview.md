# NetCraft.Game

> **完成度**：62% · **阶段**：M3-M5 进行中（主循环+世界生成+协议包+资源链路接通）· **依赖**：全部内核子库 · **计划链位置**：游戏业务层（非内核），对应原版 world/ + network/protocol/* + client/main + server/main

可选游戏业务模块，依赖 NetCraft 内核 API 实现具体游戏内容（方块/物品/实体/世界生成/协议包/DFU 升级链/文本组件等）。

对应原版 net.minecraft.client.main.Main + net.minecraft.server.Main + net.minecraft.world.* + net.minecraft.network.protocol.* + net.minecraft.network.chat.* 等游戏业务层。

## 定位

NetCraft 重点是**内核重写**，游戏业务是顺手做的事。本类库作为可选独立模块，依赖底层内核 API 实现游戏业务调度。

## 设计约束

- Game 内部只保留客户端逻辑（Minecraft 客户端本身就是 渲染+内置服务器）
- 业务调度在两个主函数中定义：ClientMain.Run（客户端）+ ServerMain.Run（服务端）
- 两个主函数物理隔离到两个独立 .cs 文件避免单一文件复杂度爆炸
- 启动参数事件驱动：内核识别的参数直接消化，未识别的通过 `LaunchOptions.UnhandledArgument` 事件广播，Game 订阅后解析
- 素材准备由 `AssetsExtractor` 辅助函数完成，核心业务直接从固定目录 `assets/` 和 `data/` 加载，不依赖辅助函数

## 实现

### 启动骨架（5 个 cs）

| 文件 | 用途 |
|------|------|
| `GameOptions.cs` | 订阅 `LaunchOptions.UnhandledArgument` 累积参数，解析 `--flag`/`--opt value`/`--opt=value`/位置参数 |
| `AssetsExtractor.cs` | 提取 jar zip 内 assets/ data/ 与根 pack.mcmeta 到固定目录，jar 不存在返回 false 不抛 |
| `ClientMain.cs` | 客户端主入口 `ClientMain.Run(args)` 串联 内核初始化+参数解析+素材提取+资源加载+客户端业务调度 |
| `ServerMain.cs` | 服务端主入口 `ServerMain.Run(args)` 串联 内核初始化+参数解析+素材提取+资源加载+服务端业务调度 |
| `ReloadableServerResources.cs` | 服务端可重载资源集合精简版持 ResourceManager + TagManager + listeners，LoadResources 静态工厂注册 TagsReloadListener 并触发首次重载 |

### Server/Client 主循环（3 个 cs）

阶段 11.35 接入 MinecraftServer/MinecraftClient 主循环空壳，阶段 11.46 接入 tick 间隔控制与 DedicatedServer 持久化关卡，阶段 11.47 接入 tick 调度链（Connection/Level/Entity），阶段 11.48 接入 ChunkSource 异步调度子系统替代 PersistentServerLevel.GetChunk 同步等待，阶段 11.49 接入 MinecraftClient 双模式主循环与 Tick 四件套，阶段 11.52 接入 ChunkGenerationHelper + DedicatedServer 可选 ChunkGenerator 参数走 ChunkStatusProcessor 生成链 + MinecraftClient.Tick 接受 delta 调 Window.Update 推进控件动画。

- `Server/MinecraftServer.cs`：抽象基类持有 Running/TickCount 状态与 ShutdownCts，Run 阻塞循环 Tick 直到 Stop，每 tick 按 Stopwatch 计算剩余时间 sleep 保持 20 TPS（SleepBudgetMillis 默认 50ms 测试可设 0/1 加速），WaitForShutdown 供外部 EXE 阻塞
- `Server/DedicatedServer.cs`：继承 MinecraftServer 持有 ServerSettings 与 LevelStorageAccess，构造时通过 GameDataFixers.BuildV1_21Fixer 构建 DataFixer 并为 OVERWORLD 维度创建 SimpleRegionStorage 与 PersistentServerLevel，阶段 11.52 加可选 ChunkGenerator/RandomSource 参数非空时调 ChunkGenerationHelper.CreateGenerator 构造 generator 闭包透传给 PersistentServerLevel 走 ChunkStatusProcessor 生成链，Tick 对齐原版 tickChildren 顺序 1.遍历玩家 Connections 调 Connection.Tick 处理入站包 2.调 Overworld.Tick 推进 ChunkSource 异步调度与实体调度 3.每 6000 tick 调 SynchronizeAsync 刷盘，Stop 时强制 flush 刷盘避免数据丢失，实现 IDisposable 释放 LevelAccess，提供 AddPlayer/RemovePlayer 接入玩家连接
- `Client/MinecraftClient.cs`：sealed 类持有 GameConfig 与 VulkanGuiApp?/Connection? 双模式 Run 分支，有 gpuApp 时 Tick 挂 FrameUpdate 事件委托 _window.Run 渲染由窗口自动完成，无 gpuApp 走 while+sleep 保持 60 FPS（SleepBudgetMillis 默认 16ms）供 Headless 测试，阶段 11.52 Tick 改签名 Tick(double delta) Headless 用 TargetFrameMillis/1000.0 作 delta，Tick 内调 PollInput + Window.Update(delta) 推进控件动画 + Connection.Tick，Stop 调 gpuApp.RequestClose 退出窗口循环，实现 IDisposable 释放 gpuApp

### Bootstrap 引导（1 个 cs）

- `Bootstrap/GameBootstrap.cs`：Game 层统一引导入口，首次调用触发 Blocks.Bootstrap + RegisterBiomes（注册 PlainsBiome 到 BuiltInRegistries.BIOME）+ DensityFunctionBootstrap.RegisterAll，幂等返回

### ItemStack 物品系统（3 个 cs）

`World/Item/ItemStack.cs`：物品栈主类对应原版 net.minecraft.world.item.ItemStack。持有 Holder<Item> + count + PatchedDataComponentMap 三元组。提供：
- `Empty` 空栈单例（_item=null）
- `OptionalStreamCodec` 允许空栈编解码
- `StreamCodec` 禁止空栈编解码
- Copy/CopyWithCount 等方法

`World/Item/DataComponents.cs`：Game 层预定义组件类型 bootstrap 注册器，`Bootstrap()` 注册 MAX_STACK_SIZE 等组件类型到 BuiltInRegistries.DATA_COMPONENT_TYPE，由 ClientMain/ServerMain 在 BootstrapClass.BootStrap 之前调用

`World/Item/FilterMask.cs`：阶段 11.53 新增组件过滤掩码对应原版 net.minecraft.world.item.component.FilterMask，持 exclusion/inclusion 两个 HashSet<object>（ReferenceEqualityComparer），提供 Add/Remove/IsFiltered/IsExplicitlyIncluded/Filter/IsEmpty，内嵌 PredicateDataComponentMap 包装视图按掩码过滤避免拷贝，待 Container 型 codec 接入时用于限定暴露给客户端的组件子集

`World/Entity/EquipmentSlot.cs`：装备槽位枚举 MAINHAND/OFFHAND/FEET/LEGS/CHEST/HEAD，ClientboundSetEquipmentPacket.Slots 当前用此枚举对齐原版

namespace 用 `NetCraft.Game.World.Items`（复数）避免与 `NetCraft.Registry.Item` 类型名冲突。

### 方块系统（2 个 cs）

- `World/Level/Block/BlockBehaviour.cs`：方块行为抽象基类对应原版 net.minecraft.world.level.block.BlockBehaviour
- `World/Level/Block/Blocks.cs`：内置方块常量与 Bootstrap 注册，注册 AIR/STONE/DIRT/GRASS_BLOCK/WATER/LAVA 到 BuiltInRegistries.BLOCK

### 客户端渲染业务（Client/Render/，规划中）

对应原版 `net.minecraft.client.renderer` 层。承载依赖 ResourceManager/BlockStateRegistry/Storage 的客户端渲染业务逻辑，与 NetCraft.Gpu（渲染原语层）分层。Gpu 提供渲染原语（GPU 抽象/pipeline/纹理图集拼接算法/BakedModel 纯数据容器），本目录提供业务逻辑（模型 JSON 解析/BlockState→模型映射/chunk mesh 生成/相机/世界渲染调度）。

- `Client/Render/Model/`：方块模型 JSON 解析+烘焙（BlockModelLoader 读 blockstates/models JSON，BlockModelBaker 烘焙 UnbakedModel→BakedModel，BlockStateModelMapper 映射 BlockState→BakedModel）
- `Client/Render/Atlas/`：纹理收集业务（扫描 blockstates/models JSON 收集 sprite 路径列表，喂给 Gpu 层 TextureStitcher 拼接）
- `Client/Render/World/`：chunk mesh 生成+相机+世界渲染调度（规划中，W3/W5/W6）

### 实体系统（4 个 cs）

- `World/Entity/EntityTypes.cs`：实体类型注册入口对应原版 net.minecraft.world.entity.EntityType
- `World/Entity/Mob.cs`：Mob 抽象基类对应原版 net.minecraft.world.entity.Mob
- `World/Entity/Player.cs`：Player 类对应原版 net.minecraft.world.entity.player.Player
- `World/Entity/EquipmentSlot.cs`：装备槽位枚举（见 ItemStack 系统）

### 世界生成系统（25 个 cs）

阶段 11.36-11.45 推进世界生成 A-E 阶段及后续，覆盖 DensityFunction 体系/NoiseChunk/Aquifer/ChunkGenerator/ChunkStatus 流水线/Climate/SurfaceRules/StructureFeature/StructurePiece/StructurePlacement/StructureSettings 等子系统。

#### LevelGen 核心（12 个 cs）

- `World/Level/LevelGen/ChunkGenerator.cs`：ChunkGenerator 真实抽象类持有 BiomeSource + 6 个抽象方法 GetGenDepth/GetBaseHeight/GetBaseColumn/FillFromNoise/BuildSurface/ApplyBiomeDecoration
- `World/Level/LevelGen/NoiseBasedChunkGenerator.cs`：基于噪声的区块生成器继承 ChunkGenerator，FillFromNoise 接入 ProtoChunk/NoiseChunk/Aquifer 真实填方块，构造前 WithSamplerIfNeeded 自动注入 NoiseRouterSampler
- `World/Level/LevelGen/NoiseGeneratorSettings.cs`：噪声生成器设置 sealed 类持有 NoiseRouter/SeaLevel/AquifersEnabled 等 9 字段 + Overworld 静态实例 + Codec 8 字段编解码
- `World/Level/LevelGen/ChunkStatusProcessor.cs`：ChunkStatusProcessor 按 ChunkStatus 调用 ChunkGenerator 对应方法，ProcessToStatus 按 13 状态顺序推进
- `World/Level/LevelGen/ChunkGenerationHelper.cs`：阶段 11.52 新增静态辅助类 CreateGenerator 把 ChunkGenerator + ChunkStatusProcessor 包装成 ServerChunkCache 需要的 `Func<ChunkPos, ChunkAccess?>` 回调，存档未命中时构造 ProtoChunk 走 ProcessToStatus(FULL) 生成新 chunk
- `World/Level/LevelGen/StructureFeatureManager.cs`：StructureFeatureManager 持有 StructureStart/StructureReference 字典按 ChunkPos 索引
- `World/Level/LevelGen/StructureManager.cs`：StructureManager 适配器包装 StructureFeatureManager 对外暴露 StructureManager API
- `World/Level/LevelGen/StructureFeature.cs`：StructureFeature 抽象基类对应原版 net.minecraft.world.level.levelgen.structure.Structure，阶段 11.45 真实接入 Settings/FindGenerationPoint/GeneratePieces/TryGenerate 派发 StructureStart
- `World/Level/LevelGen/StructurePiece.cs`：StructurePiece 抽象基类 + BoundingBoxInt 整数边界框，含 Move/PostProcess/WriteSaveData/ReadBoundingBox 真实序列化 + FromChunkPos/Intersects/Encapsulate 辅助方法
- `World/Level/LevelGen/StructurePlacement.cs`：StructurePlacement 抽象基类持有 Spacing/Separation/Salt 提供 GetPotentialChunkPos/GridHash/IsPlacementChunk + RandomSpreadStructurePlacement 子类
- `World/Level/LevelGen/StructureSettings.cs`：StructureSettings 持有 StructureFeature 到 StructurePlacement 列表映射提供 AddPlacement/GetFeaturesForChunk
- `World/Level/LevelGen/MultiNoiseBiomeSource.cs`：多噪声生物群系源 + PlainsBiome 真实子类 HasPrecipitation/Temperature/Downfall/Category 属性
- `World/Level/LevelGen/WorldGenRegion.cs`：世界生成区域访问接口接入 ServerLevel，GetChunk 优先委托到 ServerLevel.GetChunk，无 ServerLevel 时回退 in-memory 字典

#### DensityFunction 体系（4 个 cs）

- `World/Level/LevelGen/DensityFunction.cs`：DensityFunction 接口 + MapAll/MapChildren 双变换 + FunctionContext/SinglePointContext
- `World/Level/LevelGen/DensityFunctions.cs`：10 个具体实现 Constant/Noise/Mapped/Clamp/MulOrAdd/Ap2/YClampedGradient/ShiftedContext/Shift/ShiftedNoise + Marker 标记接口
- `World/Level/LevelGen/DensityFunctionCodecs.cs`：DensityFunctionCodec dispatch codec 按 "type" 字段查找子类 MapCodec
- `World/Level/LevelGen/DensityFunctionBootstrap.cs`：注册 7 个子类 MapCodec 到 DENSITY_FUNCTION_TYPE 注册表，ObjectMapCodecAdapter<T> 适配器解决 MapCodec 协变限制

#### NoiseRouter/NoiseChunk/Aquifer（3 个 cs）

- `World/Level/LevelGen/NoiseRouter.cs`：NoiseRouter 持有 14 个 DensityFunction 字段 + MapAll 递归替换 + Empty 全零路由器
- `World/Level/LevelGen/NoiseRouterCodec.cs`：手写 14 字段 NoiseRouter 编解码避免 RecordCodecBuilder 扩展
- `World/Level/LevelGen/NoiseChunk.cs`：NoiseChunk 区块噪声上下文 + Aquifer 含水层 + WrappedDensityFunction 缓存包装

#### Climate/SurfaceRules（2 个 cs）

- `World/Level/LevelGen/Climate.cs`：Climate 多噪声参数系统 + Parameter 单维度范围 + ParameterPoint 6 维度参数点 + Sampler 接口 + ConstantSampler/NoiseRouterSampler + MultiNoiseBiomeSourceParameterList + Distance 参数空间距离计算
- `World/Level/LevelGen/SurfaceRules.cs`：SurfaceRules 核心类型 Context/ConditionSource/RuleSource + BlockStateRule/IfTrue/Sequence/Not/AbovePreliminarySurface/StoneDepth 实现

#### SurfaceSystem（1 个 cs）

- `World/Level/LevelGen/SurfaceSystem.cs`：表面构建系统接入 SurfaceRules.RuleSource 真实规则派发，无 RuleSource 时走默认 GrassBlock/Dirt 替换

#### Synth 噪声合成（4 个 cs）

- `World/Level/LevelGen/Synth/ImprovedNoise.cs`：改进版 Perlin 噪声基础类
- `World/Level/LevelGen/Synth/PerlinNoise.cs`：Perlin 噪声多倍频实现，forceLegacy 分支 + forkPositional.FromHashOf 确定性派生
- `World/Level/LevelGen/Synth/SimplexNoise.cs`：Simplex 噪声实现
- `World/Level/LevelGen/Synth/NormalNoise.cs`：归一化噪声包装 PerlinNoise 双倍频差

#### LevelChunk.Serializer（1 个 cs）

- `World/Level/Chunk/LevelChunk.Serializer.cs`：LevelChunkSerializer 区块网络序列化器，Write/Read 完整往返，factory 默认 null 时回退 PalettedContainerFactory.Default，ReadBlockState/ReadBiome 优先用 factory.LookupBlockState/LookupBiome

### DFU 业务（37 个 cs）

V1_21 版本段完整移植，覆盖 1.20.2 到 1.21.4 存档升级链。

- `DFU/GameDataFixers.cs`：业务层 DFU 注册器。DFU 内核只提供框架不主动注册 Schema 或 Fix。本类按 V1_21 版本段顺序注册 13 个 Schema 与 18 个具体 Fix 类构建可用的 DataFixer（`BuildV1_21Fixer` 方法）
- `DFU/Schemas/*.cs`（13 个）：V1Foundation/V2505/V3448/V3685/V3818_3/V3825/V3938/V4059/V4067/V4300/V4306/V4307/V4312
- `DFU/Fixes/*.cs`（23 个）：21 个 V1_21 Fix 类 + 4 个抽象父类（AttributesRenameFix/DataComponentRemainderFix/NamedEntityFix/NamedEntityWriteReadFix）去重后 23 个

DFU 层 V1_21 段代码也保留在 NetCraft.DataFixer 作参考，Game 层复制一份相同代码 namespace 改为 NetCraft.Game.DFU.Fixes/NetCraft.Game.DFU.Schemas，后期 Game 业务做时直接改 Game 层代码无需重新移植。

### Network 业务包（259 个 cs）

业务数据包注册在 Game 层不在 Network 内核。Network 仅保留 StreamCodec/FriendlyByteBuf/Connection 等通用框架。

`Network/` 子目录：
- `ConnectionExtensions.cs` 业务包相关的 Connection 扩展方法（SetListenerForServerboundHandshake/InitiateServerboundStatus/LoginConnection）
- `FriendlyByteBufExtensions.cs` FriendlyByteBuf 业务扩展方法
- `GlobalUsings.cs` Network 命名空间全局引用

`Network/Protocol/` 子目录：
- `Handshake/`（5 个 cs）握手协议包，ClientIntentionPacket + HandshakeProtocols + 监听器
- `Status/`（7 个 cs）服务器列表查询包，ServerStatus + StatusProtocols + 监听器
- `Ping/`（5 个 cs）服务器 ping/pong 包
- `Login/`（14 个 cs）登录协议包，含加密握手/压缩/自定义查询
- `Common/`（21 个 cs）跨 Configuration/Play 共享包，KeepAlive/Ping/Disconnect/CustomPayload/ResourcePack/Tags/Transfer 等 13 clientbound + 6 serverbound + 2 监听器
- `Cookie/`（4 个 cs）Cookie 子协议包，CookieRequest/Response
- `Configuration/`（13 个 cs）Configuration 阶段协议包
- `Game/`（187 个 cs）Play 阶段协议包 + ClientboundLightUpdatePacketData 光照数据类型

阶段 E ClientboundLevelChunkWithLightPacket.LightData 从 object? 升级为 ClientboundLightUpdatePacketData? 真实类型，StreamCodec Encode/Decode 接入 Write/Read。

Game 通过 `[InternalsVisibleTo("NetCraft.Game")]` 访问 Network 的 internal 方法（`SetInitialInboundProtocolInternal` / `SetDisconnectListenerInternal`）实现业务包相关 Connection 扩展。

#### Play 包类型升级状态

阶段 12 节点 3 完成后，6 个 Play 包字段从 object 占位升级为 ItemStack 真实类型：
- ClientboundSetCursorItemPacket Contents
- ClientboundContainerSetSlotPacket ItemStack
- ClientboundContainerSetContentPacket Items + CarriedItem
- ServerboundSetCreativeModeSlotPacket ItemStack
- ClientboundSetPlayerInventoryPacket Contents
- ClientboundSetEquipmentPacket Slots 用 EquipmentSlot 枚举替代 int 占位

阶段 12 节点 4 MenuType 实现后，ClientboundOpenScreenPacket.Kind 从 object 升级为 MenuType 真实类型，StreamCodec 用 ContainerId(VarInt)+MenuType.StreamCodec+ComponentSerialization.StreamCodec

阶段 11.32 Component 系统完成后，9 个 Play 包字段升级为 Component 真实类型：
- 完整 StreamCodec：SetActionBarText/SetSubtitleText/SetTitleText/SystemChat/TabList（Header/Footer）
- 字段类型升级：DisguisedChat.Message/PlayerCombatKill.Message/OpenScreen.Title/SetObjective.DisplayName/PlayerChat.UnsignedContent/ServerData.Motd

阶段 E ClientboundLevelChunkWithLightPacket ChunkData 升级为 LevelChunk 真实类型接入 LevelChunkSerializer，LightData 升级为 ClientboundLightUpdatePacketData 真实类型。

### 启动流程（共用部分）

1. 创建 `GameOptions` 订阅 `LaunchOptions.UnhandledArgument`
2. 调用 `NetCraftKernel.Initialize(args)` 内核识别的参数消费，未识别的发出事件
3. `DataComponents.Bootstrap()` + `GameBootstrap.Bootstrap()` + `BootstrapClass.BootStrap()` 引导内置注册表并 Freeze
4. `options.FlushPending()` 把无值的 `--opt` 降级为 flag
4.5. `AppPaths.SetOverride()` 设置 `--output-dir` 覆盖基准目录
4.6. `AssetsExtractor.Extract(options)` 提取 jar 的 `assets/` `data/` 与根 `pack.mcmeta` 到程序根目录
4.7. 构造 `ResourceManager` 加 vanilla pack 调 `ReloadableServerResources.LoadResources` 触发 Tags 等数据驱动重载（必须在步骤 3 之后因 `BindAll` 要求 Registry 已 Freeze）
5. （Server）加载 `server.properties` / （Client）加载 `options.txt`
6. （Server）初始化 `LevelStorage` / （Client）创建 `MinecraftClient`
7. （Server）构造 `DedicatedServer` 持 `rsr` 启动主循环 / （Client）启动主循环

### 启动参数（已支持）

| 参数 | 类型 | 用途 |
|------|------|------|
| `--jar-path <path>` | Game option | jar 文件路径，提取到 `assets/jar` |
| `--sounds-dir <path>` | Game option | 已下载音频目录，复制到 `assets/sounds` |
| `--output-dir <path>` | Game option | 输出根目录，默认 `assets` |

## 文件清单（340 个 cs）

| 区域 | 数量 | 说明 |
|------|------|------|
| 启动骨架 | 5 | GameOptions/AssetsExtractor/ClientMain/ServerMain/ReloadableServerResources |
| Server/Client 主循环 | 3 | MinecraftServer/DedicatedServer/MinecraftClient |
| Bootstrap | 1 | GameBootstrap |
| ItemStack 物品系统 | 3 | World/Item/ItemStack+DataComponents World/Entity/EquipmentSlot |
| 方块系统 | 2 | BlockBehaviour/Blocks |
| 实体系统 | 3 | EntityTypes/Mob/Player |
| 世界生成 | 25 | LevelGen 12 + DensityFunction 4 + NoiseRouter/NoiseChunk 3 + Climate/SurfaceRules 2 + SurfaceSystem 1 + Synth 4 |
| LevelChunk.Serializer | 1 | LevelChunk.Serializer |
| DFU 业务 | 37 | GameDataFixers + 13 Schema + 23 Fix |
| Network 业务包 | 259 | Handshake(5)/Status(7)/Ping(5)/Login(14)/Common(21)/Cookie(4)/Configuration(13)/Game(187) + Game 含 ClientboundLightUpdatePacketData |
| Network 框架扩展 | 3 | ConnectionExtensions/FriendlyByteBufExtensions/GlobalUsings |

注：Chat 文本组件系统已迁移到 NetCraft.Network/Chat 子目录（详见 Network Overview）

## 测试

- BlockTests 5 个用例（ConcreteBlock/BlockBehaviour）
- EntityTests 5 个用例（Entity/Player/Mob/EntityTypes）
- NoiseTests 5 个用例（ImprovedNoise/PerlinNoise/SimplexNoise/NormalNoise）
- DensityFunctionTests 22 个用例（含 dispatch codec 往返）
- WorldGenTests 23 个用例（ProtoChunk/NoiseChunk/Aquifer/FillFromNoise/SurfaceSystem/WorldGenRegion/LevelChunk/Climate/MultiNoiseBiomeSource/ChunkStatus + PlainsBiome 注册）
- SurfaceRulesTests 9 个用例
- LevelChunkSerializerTests 5 个用例（含 LightData 往返）
- ServerTests 18 个用例（MinecraftServer/DedicatedServer/MinecraftClient 主循环 + tick 间隔控制 + DedicatedServer 接入 PersistentServerLevel + tick 调度 Connection/Level/Entity + ChunkSource 异步调度 + 阶段 11.52 新增 EntityLookup 范围查询/Remove + generator fallback）
- Chat/DFU/ItemStack/MenuType 等其他测试模块详见 NetCraft.Test/Overview
- 资源链路测试模块：
  - ReloadableServerResourcesTests 5 个用例（LoadResources 空 packs 不抛/TagsReloadListener 注册/Reload 重新触发/listeners 顺序/ReloadContext 序号）
  - PackMetadataSectionTests 7 个用例（FromJson 标准/缺 description/缺 pack 节点/description 为对象/缺 pack_format/Reader 返回 null/Reader 从 StubPack 读取）
  - AssetsExtractorTests 5 个用例（提取 assets+data+pack.mcmeta/跳过 class+META-INF/dataDir null 跳过 data/rootDir null 回退/jar 不存在返回 false）
- 全测试套件 831/831 全过无回归（含阶段 11.46-11.52 server 用例 + 资源链路 17 用例）

## 规划

- FilterMask 等剩余业务类型补全（替换 Play 包剩余 object 占位）
- DataComponents 预定义组件类型实现（MAX_STACK_SIZE/DAMAGE/ENCHANTMENTS 等）注册到 BuiltInRegistries.DATA_COMPONENT_TYPE
- Item 类实现 builtInRegistryHolder/components 等方法
- 实体系统扩展（LivingEntity/Animal 等更多子类）
- 阶段 11.45 已完成世界生成后续 StructureFeature/StructurePiece 真实子系统接入 StructurePlacement/StructureSettings + ChunkStatus.SimpleChunkState/StatePool + 真实 Biome codec 接入 PlainsBiome
- 阶段 11.46 已完成主循环 tick 间隔控制（Server 20 TPS / Client 60 FPS）+ DedicatedServer 接入 PersistentServerLevel 持久化关卡与自动刷盘
- 阶段 11.47 已完成 DedicatedServer.Tick 接入真实世界 Tick 调度链（Connection.Tick + Overworld.Tick + Entity.Tick）对齐原版 tickChildren 顺序
- 阶段 11.48 已完成 ChunkSource 异步调度子系统（ChunkHolder/ChunkResult/ChunkSource/ServerChunkCache/ChunkMap）替代 PersistentServerLevel.GetChunk 同步等待
- 阶段 11.49 已完成 MinecraftClient.Tick 接入客户端内核双模式（窗口驱动 + Headless）+ Tick 四件套（Input.Poll/Gui.Update 占位/Network.ProcessPackets/Gpu.Render）
- 接入内核未支持类（待内核实现 GameConfig/Settings 等完整配置加载）

注：作为可选模块依赖内核 API 稳定后逐步推进业务。
