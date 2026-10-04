# NetCraft.Interop

> **完成度**：85% · **阶段**：基础完成 · **依赖**：无 · **计划链位置**：Layer 2 互操作层，跨平台硬约束（禁 WinAPI）

Layer 2 子库，互操作层。承载与平台原生 API 的互操作封装。

所有互操作必须保持跨平台（项目硬约束禁止平台特定 API 如 WinAPI），全部基于 .NET 跨平台标准库（NativeMemory/MemoryMappedFile/NativeLibrary）。

## 实现

- `InteropRuntime` 互操作运行时工具提供 UTF-8/UTF-16 编码转换与平台信息查询
- `MemoryMappedFileAccessor` 跨平台内存映射文件访问器封装 .NET MemoryMappedFile
- `NativeLibraryAccessor` 跨平台动态库加载封装提供 Load/Free/GetDelegate 接口

## 文件清单（3 个 cs）

| 文件 | 用途 |
|------|------|
| `InteropRuntime.cs` | 互操作运行时工具 UTF-8/UTF-16 转换 + 平台信息查询 |
| `MemoryMappedFileAccessor.cs` | 内存映射文件访问器 IDisposable 封装 |
| `NativeLibraryAccessor.cs` | 动态库加载封装 Load/Free/GetDelegate |

## 测试

- InteropTests 测试覆盖跨平台互操作，详见 NetCraft.Test/Overview

## 规划

- 线程/任务调度原语跨平台抽象（按需补）
