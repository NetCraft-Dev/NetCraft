# NetCraft.Primitives

> **完成度**：85% · **阶段**：M1 已完成 · **依赖**：无（最底层）· **计划链位置**：Layer 0 基础值类型，被所有上层引用

Layer 0 子库，基础值类型集合。承载游戏世界中的纯值类型（坐标/位置/方向等）。

对应原版 `net.minecraft.core` 中的 Vec3i/BlockPos/ChunkPos/Direction 等。无任何依赖，被所有上层子库引用。

## 实现

- 所有值类型统一用 `readonly struct` 对齐 C# 惯用值语义（原版 Java 是可变类，C# 不复刻可变性）
- `BlockPos`/`SectionPos` 含 `asLong` 位运算 pack 对应原版压缩存储
- `QuartPos` 是静态工具类非 struct（仅常量与位运算）
- 坐标 `Vec3i`/`Vec3` 仅实现坐标运算与距离计算，Codec/StreamCodec 延后到 Codec/Network 接通
- `GlobalPos.dimensionKey` 用 object 占位，待 Registry 接通后改 `ResourceKey<Level>`
- `Direction` 取原版 6 个基础方向（DOWN/UP/NORTH/SOUTH/WEST/EAST）持 StepX/Y/Z 偏移量与 Axis 轴

## 文件清单（8 个 cs）

| 文件 | 用途 |
|------|------|
| `ChunkPos.cs` | 区块坐标 readonly struct 含 pack/unpack/region 方法 |
| `BlockPos.cs` | 方块位置 readonly struct 含 asLong 位运算 pack |
| `SectionPos.cs` | 区块段坐标 readonly struct 段大小 16 段内 block 坐标 4 位 |
| `QuartPos.cs` | 四分坐标静态工具类 block 坐标右移 2 位变 quart 坐标 |
| `GlobalPos.cs` | 全局位置 readonly struct 持维度 + BlockPos dimensionKey 占位 object |
| `Vec3i.cs` | 3D 整型向量 readonly struct 实现 IEquatable + IComparable |
| `Vec3.cs` | 3D 浮点向量 readonly struct 对应原版 Vec3 |
| `Direction.cs` | 方向 readonly struct 持 StepX/Y/Z + Axis 枚举 |

## 测试

- PrimitivesTests 测试覆盖 Identifier/ResourceKey/基本值类型，详见 NetCraft.Test/Overview

## 规划

- GlobalPos.dimensionKey 接入真实 `ResourceKey<Level>` 待 Registry 接通
- Vec3 Codec/StreamCodec 延后到 Network 子系统就绪
