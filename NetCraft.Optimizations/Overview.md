# NetCraft.Optimizations

> **完成度**：70%（10 个优化模块全部实现，5 个已实质集成）· **阶段**：M1-M4 部分集成 · **依赖**：跨子库查询 · **计划链位置**：Layer 4 优化集散地，对应《核心优化点.md》

Layer 4 子库，性能优化集散地。承载所有针对原版 Minecraft 的性能优化实现。

每个优化点对应《核心优化点.md》中的一项，按子文件夹独立组织（项目硬约束）。

## 实现

10 个优化模块全部完整实现，5 个已实质集成到对应子系统，5 个为开关查询 API（待子系统就绪后集成）。

## 文件清单（10 个 cs，每个独立子目录）

| 文件 | 用途 | 集成状态 |
|------|------|------|
| `BlockState/BlockStateOptimizations.cs` | BlockState 优化，对应 2.5，借鉴 FerriteCore FastMap | 已集成（NetCraft.Registry/State/BlockState.cs readonly struct） |
| `Nbt/NbtOptimizations.cs` | NBT 优化，对应 2.1/2.2 | 已集成（NbtIo.ReadCompressedWithMemoryMapped） |
| `Storage/StorageOptimizations.cs` | 存档 IO 优化，对应 2.8/2.11 | 已集成（RegionFile MMF + RegionFileStorage.OpenChunkInputStream） |
| `Registry/RegistryOptimizations.cs` | 注册表优化，对应 2.3/2.4 | 已集成（MappedRegistry.Freeze 构建 FrozenDictionary） |
| `Network/NetworkOptimizations.cs` | 网络优化，对应 2.6/2.7 | 已集成（FriendlyByteBuf.WriteVarInt Span 批量写入） |
| `PalettedContainer/PalettedContainerOptimizations.cs` | 调色板容器优化，对应 2.10 | 开关查询（SimpleBitStorage 已是 SIMD 友好 long[] 紧凑布局） |
| `Profiler/ProfilerOptimizations.cs` | 性能分析器优化，对应 2.12 | 开关查询（待 NetCraft.Util/Profiler 子系统就绪） |
| `Commands/CommandsOptimizations.cs` | 命令框架优化，对应 2.9 | 开关查询（CommandNode 保持 class 兼容派生类） |
| `Memory/MemoryOptimizations.cs` | 内存优化，借鉴 FerriteCore mrl/threaddetec/datacomponents | 开关查询（待对应子系统就绪） |
| `Gpu/GpuOptimizations.cs` | GPU 渲染优化，对应 Vulkan 后端 | 开关查询（待 NetCraft.Gpu 子系统就绪） |

## API 模式

每个优化模块统一提供：
- `Is{Feature}Enabled`：查询对应 OptimizationFlags 开关状态
- `IsOptimized`：检查所有相关开关是否全开
- `GetStats()`：返回 `{Module}OptimizationStats` record struct 用于诊断

## 集成模式

实质集成的模块采用最小改动策略：
- 保留原 API 不变，新增按开关选择的优化路径
- 失败时降级到原路径保证语义一致
- 测试覆盖开关状态、统计 API 和实际功能验证（字节级 round-trip）

## 测试

35 个 OptimizationsTests 测试全部通过，覆盖所有 10 个模块的开关查询和实质集成功能。

