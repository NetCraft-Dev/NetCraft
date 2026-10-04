# NetCraft.Codec

> **完成度**：70% · **阶段**：M2 已完成最小集 · **依赖**：无 · **计划链位置**：Layer 1 序列化框架，NBT/DFU/Network 的基础

Layer 1 子库，Mojang DFU（com.mojang.serialization）的 C# 移植最小集。

提供对象与任意类型（DynamicOps<T>）之间的序列化/反序列化框架。NBT 序列化通过 NbtOps（在 NetCraft.Nbt）实现 DynamicOps<Tag>。

## 实现

- Codec<T> 主序列化接口继承 MapCodec<T>
- MapCodec<T> map 字段序列化接口含 EncodeTo 累积字段到 RecordBuilder
- AbstractMapCodec<T> 抽象基类提供 EncodeStart/Parse 默认实现基于 EncodeTo + MapBuilder
- ScalarCodec<T> 标量 codec 抽象基类EncodeTo 默认抛 NotSupportedException
- DataResult<T> 成功/失败结果
- DynamicOps<T> 抽象类型操作接口含 MapBuilder 创建 RecordBuilder
- MapLike<T> 抽象 map 视图
- RecordBuilder<T> 抽象 record builder
- Pair<TFirst,TSecond> 简二元组（含 Deconstruct）
- Optional<T> 轻量 Optional
- Dynamic<T> 包装类
- 基础标量 Codec（Byte/Short/Int/Long/Float/Double/Bool/String）
- ListCodec/ComapFlatMapCodec 组合子
- FieldCodec<T,F> 字段定义包装 MapCodec 与 getter
- FieldMapCodec/OptionalFieldMapCodec/OptionalFieldMapCodecOptional 三种 fieldOf 变体
- RecordCodecBuilder.Of2/Of3/Of4 N-ary 重载模拟原版 group(...).apply(instance, ctor)
- CodecExtras 含 LongArrayCodec/MapResultCodec/LenientOptionalFieldCodec 三种扩展组合子对应原版 LONG_STREAM/orElsePartial/lenientOptionalFieldOf
- DataResult.GetOrThrow 重载支持自定义异常工厂对应原版 getOrThrow(ChunkReadException::new)
- ObjectOpsAdapter 把 DynamicOps<T> 适配为 DynamicOps<object> 对齐 Java 类型擦除下 DynamicOps<?> 通配语义
- OptionalDynamic 包装 Dynamic.Get 的 DataResult 提供 AsNumber/AsString 等便捷方法

## 文件清单（17 个 cs）

| 文件 | 用途 |
|------|------|
| `Pair.cs` | 简二元组对应 com.mojang.datafixers.util.Pair |
| `Optional.cs` | 轻量 Optional 对应 java.util.Optional |
| `DataResult.cs` | 成功/失败结果对应 DataResult |
| `MapLike.cs` | 抽象 map 视图对应 MapLike |
| `DynamicOps.cs` | 抽象类型操作接口对应 DynamicOps 含 MapBuilder |
| `RecordBuilder.cs` | 抽象 record builder 对应 RecordBuilder |
| `MapCodec.cs` | map 字段序列化接口对应 MapCodec 含 EncodeTo |
| `Codec.cs` | 主序列化接口与 ScalarCodec 基类与 AbstractMapCodec |
| `Dynamic.cs` | 动态值包装对应 Dynamic |
| `OptionalDynamic.cs` | 可选 Dynamic 包装 DataResult 转发 AsNumber/AsString |
| `ObjectOpsAdapter.cs` | DynamicOps<T> 适配为 DynamicOps<object> 跨 T 通配 |
| `Codecs.cs` | 基础标量 Codec 与组合子工厂 |
| `FieldCodec.cs` | 字段定义 FieldCodec<T,F> 与 ForGetter 扩展 |
| `FieldCodecs.cs` | fieldOf 实现含 FieldMapCodec 与两种 OptionalFieldMapCodec |
| `CodecExtensions.cs` | FieldOf/OptionalFieldOf 扩展方法 |
| `CodecExtras.cs` | LongArray/MapResult/LenientOptionalFieldOf 扩展组合子 |
| `RecordCodecBuilder.cs` | RecordCodecBuilder.Of2/Of3/Of4 与 RecordCodec2/3/4 |

## 设计决策

- Number 简化为 double 原版 getNumberValue 返回 Number
- ListCollector 简化为 GenericListCollector 不做 Byte/Int/Long 紧凑优化
- 不实现 CompoundTag.CODEC 原版用 PASSTHROUGH.comapFlatMap 暂不在 store/read 链路
- NbtOps 留在 NetCraft.Nbt 让 Nbt 引用 Codec 形成单向依赖
- 不实现完整 HKT 原版 App<Mu,T> 模式用 Of2/Of3/Of4 N-ary 重载覆盖常见 record
- AbstractMapCodec 实现 EncodeStart 默认基于 EncodeTo + MapBuilder.Build(empty) 子类只需 Decode + EncodeTo
- OptionalFieldMapCodec 三种变体必填/带默认值/返回 Optional<T>

## 规划

- Of5..Of8 重载按需补
- 完整 Codec 组合子（pair/alternative/numericRange 等）随用随补
- CompoundTag.CODEC 与完整 DFU 链路延后到 SerializableChunkData 一起做
