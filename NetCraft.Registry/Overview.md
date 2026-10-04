# NetCraft.Registry

> **完成度**：90% · **阶段**：M3 已达成（注册表+BlockState）· **依赖**：NetCraft.Primitives + NetCraft.Codec · **计划链位置**：Layer 2 注册表框架，对应原版 core/ (103 文件)

Layer 2 子库，注册表系统。Minecraft 的全局内容注册中心，所有方块/物品/实体/方块实体/状态等都通过此系统注册。

对应原版 `net.minecraft.core` + `net.minecraft.core.registries` 包，API 语义对齐 Minecraft 26.2。

## 实现

### 注册表核心

- Identifier 值类型（26.2 由 ResourceLocation 重命名，readonly struct 零堆分配）
- ResourceKey<T> 资源键，per-T intern 池去重
- IdMap<T>/IdMapper<T> ID 与值双向映射
- Holder<T> 体系（Direct 直接包装值、Reference 注册表引用两阶段绑定）
- HolderLookup<T>/HolderLookupProvider 元素查找与跨注册表查找入口 Registry<T> 继承 HolderLookup<T>
- HolderSet<T> 一组 Holder 按标签或直接列表形式
- Registry<T>/WritableRegistry<T> 注册表接口
- MappedRegistry<T> 核心实现，6 张映射表（byId/byLocation/byKey/byValue/toId/registrationInfos）
- DefaultedRegistry<T>/DefaultedMappedRegistry<T> 带默认值注册表
- RegistryAccess/Frozen/ImmutableRegistryAccess 跨注册表访问入口 继承 HolderLookupProvider
- TagKey<T> 标签键 intern 池
- 148 个 Registries 常量 + 95 个 BuiltInRegistries 注册表实例字段
- RegistryOps 注册表感知的 DynamicOps 装饰器，包装底层 DynamicOps 持有 RegistryAccess 提供 Codec 解析时的注册表查询入口

### DataComponent 子系统

数据组件接口层位于 Registry，实现层位于 Network（详见 NetCraft.Network Overview 的 Component 子目录）。Registry 不依赖 Network，故 DataComponentType 接口只含 Codec 不含 StreamCodec。

- `DataComponentType` 接口含 Codec/IgnoreSwapAnimation/IsTransient/CodecOrThrow
- `TypedDataComponent<T>` record 持 DataComponentType<T> 与 T value
- `DataComponentMap` 继承 DataComponentLookup 只读接口 + Composite 静态构造 + EmptyDataComponentMap + CompositeDataComponentMap + EmptyDataComponentLookup

### State 子系统（8 个 cs）

- PropertyBase 非泛型基类 + Property<T> 泛型类
- BooleanProperty/IntegerProperty 具体属性实现
- PropertyValue 属性值绑定 record
- StateHolder 状态持有者基类，管理属性值数组与 O(1) setValue
- StateDefinition 笛卡尔积枚举所有状态并构建 neighbors 邻居表
- BlockState 继承 StateHolder<Block, BlockState>
- BlockStateDefinition 非泛型版本专门构建 BlockState struct
- BlockStateRegistry 所有 BlockState 实例的属性数据与 neighbors 集中存储，避免每实例持有数组（对齐 FerriteCore FastMap 思路，struct BlockState 只持 int Id 数据查表，对应优化点 2.5）

### Codec 子目录（2 个 cs）

- IdentifierCodec：Identifier codec 序列化为 StringTag 用 namespace:path 格式
- StringMapCodec：string map codec 对应原版 Codec.unboundedMap(Codec.STRING, Codec.STRING)

### Game 子目录（6 个 cs）

抽象基类对应原版游戏业务类型，子类按需扩展。 stub 类型暂存此处待 Game 子系统就绪后迁移。

- Block 抽象基类对应原版 net.minecraft.world.level.block.Block
- Item 抽象基类对应原版 net.minecraft.world.item.Item
- Entity 抽象基类对应原版 net.minecraft.world.entity.Entity
- EntityType<T> 抽象基类对应原版 net.minecraft.world.entity.EntityType
- Fluid 抽象基类对应原版 net.minecraft.world.level.material.Fluid
- Biome 抽象基类对应原版 net.minecraft.world.level.biome.Biome

### stub 类型

- _GameStubs.cs 124 个 stub 游戏类型（待具体子系统就绪后迁移）
- _ChunkStubs.cs ChunkStatus stub 对应原版 net.minecraft.world.level.chunk.status.ChunkStatus

阶段 E 已删除被真实实现取代的 stub 接口：
- SurfaceRulesConditionSource/SurfaceRulesRuleSource 已由 NetCraft.Game/World/Level/LevelGen/SurfaceRules.cs 真实接口取代，MATERIAL_CONDITION/MATERIAL_RULE 改为 `MapCodec<object>` 弱类型对齐跨层方案
- MultiNoiseBiomeSourceParameterList 已由 NetCraft.Game/World/Level/LevelGen/Climate.cs 真实类取代，MULTI_NOISE_BIOME_SOURCE_PARAMETER_LIST 改为 `Registry<object>` 弱类型
- 同阶段 D CHUNK_GENERATOR/NOISE_SETTINGS/DENSITY_FUNCTION_TYPE 已用 `MapCodec<object>`/`object` 弱类型跨层

MappedRegistry.Register 显式调用 holder.BindValue(value) 绑定值，避免 Reference&lt;T&gt; 未绑定值导致 "Trying to access unbound value" 异常。

## 文件清单（41 个 cs）

### 根目录（25 个 cs，注册表核心 + DataComponent 接口 + stub）

| 文件 | 用途 |
|------|------|
| `Identifier.cs` | 标识符值类型（namespace:path，校验/Parse/Equality） |
| `ResourceKey.cs` | 资源键，per-T intern 池，Create/Cast/Dependent |
| `ResourceKeys.cs` | 跨 Registry 工厂方法（CreateRegistryKey<T>） |
| `IdMap.cs` | ID↔值映射接口 + IdMapper<T> 简单实现 |
| `Holder.cs` | Holder<T> 接口，Direct/Reference 两类 |
| `HolderLookup.cs` | HolderLookup<T> 元素查找 + HolderLookupProvider 跨注册表查找 |
| `HolderOwner.cs` | Holder 所有者标记接口 |
| `HolderSet.cs` | HolderSet<T> 接口，按标签或直接列表形式 |
| `TagKey.cs` | 标签键 intern 池 |
| `Lifecycle.cs` | 注册项稳定性标记（Stable/Experimental） |
| `KnownPack.cs` | 资源包来源 stub record |
| `RegistrationInfo.cs` | 注册元信息 record |
| `RegistryOps.cs` | 注册表感知 DynamicOps 装饰器 |
| `DataComponentType.cs` | 数据组件类型接口含 Codec 等元数据 |
| `TypedDataComponent.cs` | 带类型的组件值 record |
| `DataComponentMap.cs` | 数据组件映射接口 + Empty/Composite 实现 |
| `Registry.cs` | 注册表接口 + WritableRegistry<T> |
| `MappedRegistry.cs` | 核心实现，6 张映射表 + Freeze 校验 |
| `DefaultedRegistry.cs` | 带默认值注册表接口 |
| `DefaultedMappedRegistry.cs` | 带默认值实现，未注册 key 回退 default |
| `RegistryAccess.cs` | RegistryAccess/Frozen/ImmutableRegistryAccess/RegistryEntry |
| `Registries.cs` | 148 个 ResourceKey 常量集合 |
| `BuiltInRegistries.cs` | 95 个注册表实例字段 + RegisterSimple/RegisterDefaulted |
| `_GameStubs.cs` | 124 个 stub 游戏类型（待迁移，阶段 E 已删 3 个被真实实现取代的接口） |
| `_ChunkStubs.cs` | ChunkStatus stub |

### Codec 子目录（2 个 cs）

| 文件 | 用途 |
|------|------|
| `IdentifierCodec.cs` | Identifier codec 序列化为 StringTag |
| `StringMapCodec.cs` | string map codec 对应 unboundedMap(STRING,STRING) |

### Game 子目录（6 个 cs，游戏业务抽象基类）

| 文件 | 用途 |
|------|------|
| `Block.cs` | 方块抽象基类 |
| `Item.cs` | 物品抽象基类 |
| `Entity.cs` | 实体抽象基类 |
| `EntityType.cs` | 实体类型抽象基类 |
| `Fluid.cs` | 流体抽象基类 |
| `Biome.cs` | 生物群系抽象基类 |

### State 子目录（8 个 cs，方块状态框架）

| 文件 | 用途 |
|------|------|
| `Property.cs` | 属性抽象，PropertyBase + Property<T> |
| `Properties.cs` | BooleanProperty、IntegerProperty |
| `PropertyValue.cs` | 属性值绑定 record |
| `StateHolder.cs` | 状态持有者基类，O(1) setValue |
| `StateDefinition.cs` | 笛卡尔积枚举状态构建 neighbors |
| `BlockState.cs` | 方块状态类，继承 StateHolder<Block, BlockState> |
| `BlockStateDefinition.cs` | 非泛型版本专门构建 BlockState struct |
| `BlockStateRegistry.cs` | BlockState 数据集中存储查表（优化点 2.5） |

## 测试

- 已合并到 `NetCraft.Test`（`Modules/RegistryTests.cs`，45 个用例覆盖 Identifier/ResourceKey/Registry/MappedRegistry/DefaultedMappedRegistry/RegistryAccess/BuiltInRegistries/TagKey/HolderLookup/DataComponentLookup/GetRandom/CreateRegistrationLookup + State 框架），详见 `NetCraft.Test/Overview.md`

## 完成状态与剩余依赖

- tag 子系统 Registry 端已完整（BindTags/Get/GetTags/Holds/NamedHolderSet/DirectHolderSet/_allTags/_allTagsFrozen），数据驱动加载由 NetCraft.Tags 子库 TagLoader/TagManager + Bootstrap.LoadBuiltinTags 承担
- HolderLookup.Provider 已完成（HolderLookup<T>/HolderLookupProvider 接口 + Registry<T> 继承 HolderLookup<T> + RegistryAccess 继承 HolderLookupProvider）
- Registry.GetRandom(RandomSource) 已完成（按 byId 索引随机返回 Holder 空注册表返回 null）
- WritableRegistry.CreateRegistrationLookup 已完成（返回 SingleRegistryLookupProvider<TRegistry> 单注册表 HolderLookupProvider 视图）
- intrusive holders 已完整（Reference.CreateIntrusive + MappedRegistry._intrusiveHolders + CreateIntrusiveHolder + Register 复用 BindKey）
- RandomSource 子系统已完成（NetCraft.Util/Random: Xoroshiro128++/PositionalRandomFactory/MarsagliaPolarGaussian/WeightedRandom）
- propertiesCodec、EnumProperty 已完成（StateDefinitionCodecs.PropertiesCodec + EnumProperty<T>）
- DataComponentLookup 已完成（非泛型接口 + DataComponentMap 继承）待 Network 层注册预定义组件类型
- bootstrap 回调（stub 类型无内容）
- 待具体游戏类型子系统就绪后把 _GameStubs.cs 与 _ChunkStubs.cs 里的 stub 类型迁移到对应项目
