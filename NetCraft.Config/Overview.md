# NetCraft.Config

> **完成度**：100% · **阶段**：M1 已完成 · **依赖**：无 · **计划链位置**：Layer 0 全局常量，编译期内联零开销

Layer 0 子库，全局配置常量。对应原版 `net.minecraft.SharedConstants`。

## 实现

- 版本/协议号与世界数据版本等不可变常量
- 修复行为开关（默认值反映是否启用对应 bug 修复）
- 性能优化开关（每个开关对应《核心优化点.md》一个优化点）
- 调试标志（仅 Debug 构建有意义，Release 编译器消除不可达分支）

所有成员为 `const`，编译期内联，零运行时开销。

## 文件清单（4 个 cs）

| 文件 | 用途 |
|------|------|
| `SharedConstants.cs` | 全局常量：版本号、协议号、世界数据版本 |
| `Fixes.cs` | 修复行为开关（TNT 复制、沙子复制、刷线机等） |
| `Optimizations.cs` | 性能优化开关（NBT Codec Source Generator、MemoryMappedFile 等） |
| `DebugFlags.cs` | 调试标志（IsRunningInIde、严格 freeze 检查等） |
