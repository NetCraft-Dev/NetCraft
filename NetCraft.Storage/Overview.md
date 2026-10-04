# NetCraft.Storage

> **完成度**：90% · **阶段**：M2 已达成（字节级兼容）· **依赖**：NetCraft.Nbt + NetCraft.Registry + NetCraft.Util · **计划链位置**：Layer 2 存档 IO，PoC 起点之一（对应原版 storage 156 文件）

Layer 2 子库，存档 IO 子系统。计划承载区块/世界/玩家数据的持久化。

对应原版 `net.minecraft.world.level.storage` + `net.minecraft.world.level.chunk.storage` 包。依赖 NBT（已完成）和 Registry（已完成）。

## 实现

MCA 文件读写层 + PalettedContainer 调色板容器 + SerializableChunkData 区块序列化均已完成。

### MCA 文件读写层

- `RegionBitmap` 扇区位图，动态 long[] 实现，支持 Force/Free/Allocate
- `RegionStorageInfo` 区域存储元信息 record
- `RegionFileVersion` 压缩版本，GZIP/DEFLATE/NONE 内置，LZ4 暂 stub
- `RegionFile` MCA 文件读写核心，FileStream 加大端 header，支持内部存储和外部 .mcc
- `RegionFileStorage` 多 RegionFile 的 LRU 缓存管理，提供 CompoundTag 读写

### BitStorage 位存储

- `BitStorage` 接口，紧凑位存储
- `SimpleBitStorage` 基础实现，按 bits 打包 long[]，支持 get/set/unpack/getRaw
- `ZeroBitStorage` 全零位存储，零位宽场景专用

### Paletted 调色板容器

- `Palette<T>` 接口，管理 storage int id 与 T 值映射
- `PaletteResize<T>` 扩容回调接口
- `Configuration` 接口 + `SimpleConfiguration`/`GlobalConfiguration` 两种实现
- `Strategy<T>` 抽象类 + `BlockStatesStrategy`/`BiomesStrategy` 两种策略
- `GlobalPalette`/`HashMapPalette`/`LinearPalette`/`SingleValuePalette` 四种调色板实现
- `CrudeIncrementalIntIdentityHashBiMap` HashMapPalette 用的哈希表
- `PalettedContainer<T>` 核心，结合 Palette 与 BitStorage 实现紧凑存储，含 Pack/Unpack 序列化与 CreateCodec
- `PalettedContainerFactory` 抽象工厂 + `DefaultPalettedContainerFactory` 默认实现，注册 Block/Biome 并提供 codec
- `PackedData<T>` record，序列化中间表示

### Chunk 区段辅助

- `LevelChunkSection` 区段，持有 BlockState 与 Holder<Biome> 两个 PalettedContainer
- `DataLayer` 光照数据，4bit 紧凑存储 4096 个值
- `PackedTicks` stub，存储方块与流体 tick 的 CompoundTag 列表
- `LevelHeightAccessor` 区段 Y 范围接口 + `SimpleLevelHeightAccessor` 简单实现
- `ChunkReadException` 区块反序列化失败异常

### SerializableChunkData 区块序列化

- `SerializableChunkData` 区块数据序列化中间表示，含 18 字段 + `Write()`/`Parse()` 完整 round-trip + `SectionData` 嵌套 record
- `CopyOf`/`Read` 阶段 11.45 完整实现，CopyOf 接受可选 factory 参数，Read 用 LevelHeightAccessor.SectionsCount 重建 chunk 保持原区段数

### Level 关卡访问层（12 个 cs）

阶段 11.34+11.42 接入 ServerLevel 具体子类与 ChunkAccess/PoiManager，阶段 11.45 新增 DirectoryLock 与 PersistentServerLevel，阶段 11.48 新增 ChunkSource 异步调度子系统（UnloadedChunkException/ChunkResult/ChunkHolder/ChunkSource/ServerChunkCache/ChunkMap），阶段 11.52 新增 EntityLookup 分区索引替代 List<Entity> + ServerChunkCache 加 generator fallback 走 ChunkStatusProcessor 生成链。

- `Level/ChunkAccess.cs` 区块访问抽象基类对应原版 net.minecraft.world.level.chunk.ChunkAccess，持有 ChunkPos/SectionData 字典/Heightmaps，提供 GetSection/SetBlockState 等接口
- `Level/PoiManager.cs` PoiManager 占位 stub 对应原版 net.minecraft.world.level.chunk.ChunkPos
- `Level/ServerLevel.cs` 服务端关卡抽象基类对应原版 net.minecraft.server.level.ServerLevel，声明 Dimension/DataVersion/RegistryAccess 抽象属性 + GetChunk(ChunkPos) 抽象方法
- `Level/SimpleServerLevel.cs` ServerLevel 基线实现持有 in-memory ChunkAccess 字典，提供 AddChunk/GetChunk(chunkX,chunkZ)，去掉 sealed 允许 PersistentServerLevel 继承复用缓存
- `Level/DirectoryLock.cs` 目录锁对应原版 net.minecraft.world.level.storage.DirectoryLock，用 session.lock 文件做独占防止多进程同时打开同一世界，跨平台走 FileStream.Lock 托管 API
- `Level/PersistentServerLevel.cs` 持久化服务端关卡继承 SimpleServerLevel 实现 LevelHeightAccessor，持有 SimpleRegionStorage 与 PalettedContainerFactory，GetChunk 缓存未命中同步等待 LoadChunkAsync，SaveChunkAsync 把 ChunkAccess 序列化写入 RegionFileStorage，阶段 11.47 接入 Tick 调度实体，阶段 11.48 接入 ServerChunkCache 异步调度，阶段 11.52 持有 EntityLookup 替代 List<Entity> + 接受可选 generator 透传给 ServerChunkCache

### Chunk 区段辅助

- `LevelChunkSection` 区段，持有 BlockState 与 Holder<Biome> 两个 PalettedContainer
- `DataLayer` 光照数据，4bit 紧凑存储 4096 个值
- `ProtoChunk` 原型区块对应原版 net.minecraft.world.level.chunk.ProtoChunk，继承 ChunkAccess 持有 BlockState/Biome 容器与 SetBlockState 真实写入
- `PackedTicks` stub，存储方块与流体 tick 的 CompoundTag 列表
- `LevelHeightAccessor` 区段 Y 范围接口 + `SimpleLevelHeightAccessor` 简单实现
- `ChunkReadException` 区块反序列化失败异常

### LevelGen 高度图（1 个 cs）

- `LevelGen/Heightmap.cs` 高度图对应原版 net.minecraft.world.level.levelgen.Heightmap，枚举类型 + QueueTracker + 逻辑

### SavedData 维度数据（2 个 cs）

- `SavedData/SavedData.cs` 维度数据基类对应原版 net.minecraft.world.level.storage.SavedData，提供 setDirty/load/save 接口
- `SavedData/SavedDataType.cs` 维度数据类型枚举

### Storage ValueIO 适配层（4 个 cs）

- `Storage/TagValueInput.cs` TagInput 适配 ValueInput 接口
- `Storage/TagValueOutput.cs` TagOutput 适配 ValueOutput 接口
- `Storage/ValueInput.cs` ValueInput 抽象接口对应原版 net.minecraft.world.level.storage.ValueInput
- `Storage/ValueOutput.cs` ValueOutput 抽象接口对应原版 net.minecraft.world.level.storage.ValueOutput

### IOWorker 异步调度层

- `ChunkScanAccess` 区块扫描接口，支持流式 visitor 不构建完整 Tag
- `IOWorker` 异步区块 IO 调度器，通过 PriorityConsecutiveExecutor 串行调度同步 RegionFileStorage，含 PendingStore 合并写入 + 三级优先级抢占（FOREGROUND/BACKGROUND/SHUTDOWN）
- blending_data 扫描已解锁：IsOldChunkAround 通过 regionCacheForBlender（LRU 1024）缓存 region 级 BitSet，CreateOldDataForRegion 用 CollectFields 仅取 DataVersion + blending_data 两字段扫描 1024 个 chunk，IsOldChunk 判定 DataVersion < 4882 或含 blending_data 字段

### SimpleRegionStorage 通用区域存储

- `SimpleRegionStorage` 持有 IOWorker 封装，对外提供 Read/Write/Synchronize/ChunkScanner/StorageInfo/Dispose 等高层 API
- UpgradeChunkTag 错误路径与原版对齐：try 块调用 DataFixer 升级路径，DataFixer 未实现抛 NotSupportedException 被 catch 包装为 ReportedException（含 "Updated chunk" 标题与 Data version/Target version 详情）
- InjectDatafixingContext/RemoveDatafixingContext 真实实现，常量 DatafixerContextTag = "__context" 对齐原版 ChunkHeightAndBiomeFix.DATAFIXER_CONTEXT_TAG
- 构造简化为不持有 DataFixer/DataFixTypes 字段（这俩类未移植），等 DataFixer 移植后补回字段与构造参数

### LevelStorage 世界存储入口

- `LevelStorage` 世界存储入口对应原版 net.minecraft.world.level.storage.LevelStorageSource，按世界名管理 LevelStorageAccess 实例，提供 CreateAccess/ListWorlds/WorldExists/DeleteWorld
- `LevelStorageAccess` 单个世界的存储访问入口对应原版 LevelStorageAccess，持有 worldDir 提供按维度获取路径和创建 SimpleRegionStorage 的能力
- `LevelKeys` 预定义维度 ResourceKey<Level> 对应原版 net.minecraft.world.level.Level.OVERWORLD/NETHER/END
- GetDimensionPath 路径约定：overworld 直接是 worldDir，其他维度在 worldDir/dim_<name>
- 阶段 11.45 接入 DirectoryLock 用 session.lock 防多进程同时操作，CreateAccess 加 acquireLock 参数兼容测试
- 已接入 ServerMain 步骤 6 初始化世界存储

## 文件清单（55 个 cs）

| 目录 | 文件 | 用途 |
|------|------|------|
| 根 | `RegionBitmap.cs` | 扇区位图，记录 MCA 文件已占用扇区 |
| 根 | `RegionStorageInfo.cs` | 区域存储元信息 record |
| 根 | `RegionFileVersion.cs` | MCA 压缩版本枚举 |
| 根 | `RegionFile.cs` | MCA 文件读写核心 |
| 根 | `RegionFileStorage.cs` | 多 RegionFile 的 LRU 缓存 |
| 根 | `SerializableChunkData.cs` | 区块数据序列化中间表示 + Write/Parse |
| 根 | `ChunkScanAccess.cs` | 区块扫描接口 |
| 根 | `IOWorker.cs` | 异步 IO 调度器 |
| 根 | `SimpleRegionStorage.cs` | 通用区域存储入口封装 IOWorker |
| 根 | `LevelStorage.cs` | 世界存储入口对应原版 LevelStorageSource |
| 根 | `LevelStorageAccess.cs` | 单世界访问入口 + LevelKeys 预定义维度 |
| 根 | `ChunkEntities.cs` | 区块实体聚合 |
| 根 | `EntityStorage.cs` | 实体存储 |
| 根 | `EntityPersistentStorage.cs` | 实体持久化存储 |
| 根 | `SavedDataStorage.cs` | 维度数据存储 |
| Level | `ChunkAccess.cs` | 区块访问抽象基类 |
| Level | `PoiManager.cs` | PoiManager 占位 stub |
| Level | `ServerLevel.cs` | 服务端关卡抽象基类 |
| Level | `SimpleServerLevel.cs` | ServerLevel in-memory 基线实现可继承 |
| Level | `DirectoryLock.cs` | 目录锁用 session.lock 独占防多进程 |
| Level | `PersistentServerLevel.cs` | 持久化服务端关卡接入 RegionFileStorage |
| Level | `UnloadedChunkException.cs` | 区块未加载异常 ChunkResult 右侧 |
| Level | `ChunkResult.cs` | 区块加载结果包装 Either |
| Level | `ChunkHolder.cs` | 区块持有器 ticket level |
| Level | `ChunkSource.cs` | 区块源抽象基类 |
| Level | `ServerChunkCache.cs` | 服务端区块缓存异步调度 |
| Level | `ChunkMap.cs` | 玩家视距管理 |
| LevelGen | `Heightmap.cs` | 高度图类型 + QueueTracker |
| BitStorage | `BitStorage.cs` | 位存储接口 |
| BitStorage | `SimpleBitStorage.cs` | 基础位存储实现 |
| BitStorage | `ZeroBitStorage.cs` | 全零位存储 |
| Chunk | `DataLayer.cs` | 光照数据 4bit 紧凑存储 |
| Chunk | `LevelChunkSection.cs` | 区段含 BlockState/Biome 容器 |
| Chunk | `ProtoChunk.cs` | 原型区块继承 ChunkAccess 真实写入 |
| Chunk | `PackedTicks.cs` | tick 数据 stub |
| Chunk | `LevelHeightAccessor.cs` | 区段 Y 范围接口 |
| Chunk | `ChunkReadException.cs` | 区块反序列化失败异常 |
| Paletted | `Palette.cs` | 调色板接口 |
| Paletted | `PaletteResize.cs` | 扩容回调接口 |
| Paletted | `Configuration.cs` | 配置接口 + Simple/Global |
| Paletted | `Strategy.cs` | 策略抽象类 + Block/Biome 策略 |
| Paletted | `GlobalPalette.cs` | 全局调色板 |
| Paletted | `HashMapPalette.cs` | 哈希调色板 |
| Paletted | `LinearPalette.cs` | 线性调色板 |
| Paletted | `SingleValuePalette.cs` | 单值调色板 |
| Paletted | `CrudeIncrementalIntIdentityHashBiMap.cs` | 哈希表实现 |
| Paletted | `PalettedContainer.cs` | 核心容器 + CreateCodec |
| Paletted | `PalettedContainerFactory.cs` | 抽象工厂 + 默认实现 + Unpack 反序列化 |
| SavedData | `SavedData.cs` | 维度数据基类 |
| SavedData | `SavedDataType.cs` | 维度数据类型枚举 |
| Storage | `TagValueInput.cs` | TagInput 适配 ValueInput |
| Storage | `TagValueOutput.cs` | TagOutput 适配 ValueOutput |
| Storage | `ValueInput.cs` | ValueInput 抽象接口 |
| Storage | `ValueOutput.cs` | ValueOutput 抽象接口 |

注：CrudeIncrementalIntIdentityHashBiMap 已修复值类型槽位占用判断（用 bool[] 标记），SingleValuePalette 用 _hasValue 标记初始值。PalettedContainerFactory 新增 UnpackBlockStates/UnpackBiomes 反序列化接口 + LookupBlockState/LookupBiome 查找方法，RegisterBlock/RegisterBiome 改为 ??= 仅首次设置默认值对齐原版 AIR 默认。

## 规划

- DataFixer 子系统移植（接通 UpgradeChunkTag 真实升级路径，替换 try 块内的 NotSupportedException）
- `DimensionDataStorage` 维度数据存储与 LevelStorageAccess 整合
- ChunkStatus 状态机真实生成链接入（11.52 已接通 ServerChunkCache generator fallback 走 ChunkStatusProcessor，但各 status 子系统仍 stub 待 NoiseBiome/Carver/Feature 就绪）
- ServerLevel.EntityLookup 分区索引查询替代 List<Entity>（11.52 已实现 EntityLookup + PersistentServerLevel 接入）
- 内核层 LevelStorage 集成 RegionFileStorage 接入主循环
