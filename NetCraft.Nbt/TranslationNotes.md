# NetCraft.Nbt 翻译笔记

对应原版 `net.minecraft.nbt` 包。NBT 是存档与协议序列化的底层依赖，是重写计划阶段 2 的 PoC 起点。

## 翻译进度

### 已完成（24/24 round-trip 测试通过）

- `Tag` / `TagType` / `TagVisitor` / `StreamTagVisitor` 核心接口
- `NumericTag` 体系：`EndTag` / `ByteTag`(256 缓存) / `ShortTag` / `IntTag` / `LongTag` / `FloatTag` / `DoubleTag`
  - 均实现 `ValueOf` 工厂 + `Copy() => this`（不可变，对齐原版）
  - 均重写 `Equals` / `GetHashCode`（对齐原版 equals 语义；float/double 用 bits 比较避免 NaN 问题）
- `CollectionTags`：`StringTag`(Empty 单例) / `ByteArrayTag` / `IntArrayTag` / `LongArrayTag`
  - 数组类均用 `AsSpan().SequenceEqual` 比较
- `CompoundTag`：两阶段 `Accept` + 三阶段 `Parse`（VisitEntry 类型 → 名字 → 递归 Parse），`SkipToEndOfCompound` 辅助
- `ListTag`：`VisitElement` + `VisitContainerEnd`，完整 BREAK/SKIP/Halt 语义 + 剩余元素跳过
- `NbtAccounter`：可变 `_depth`，`PushDepth` / `PopDepth`，`AccountBytes(overhead, count)` 重载
- `NbtIo`：`ReadUnnamedTag` / `WriteUnnamedTag` / `ReadCompressed` / `WriteCompressed` / `Parse` / `ParseCompressed`
- `NbtException` / `NbtFormatException` / `ReportedNbtException`（简化版，不依赖 CrashReport）
- `INbtReader` / `INbtWriter` + `BinaryNbtReader` / `BinaryNbtWriter`（大端，Java modified UTF-8）
- `NbtUtils`：`CompareNbt` / `PrettyPrint` / `AddDataVersion` / `GetDataVersion` / `PackBlockState` / `UnpackBlockState`
- visitors：`SkipAll` / `CollectToTag` / `SkipFields` / `CollectFields` / `FieldSelector`(record) / `FieldTree`
- `Number` struct（对应 `java.lang.Number`，double 包装）

### 延后（依赖其他子系统）

| 类 | 依赖 | 备注 |
|----|------|------|
| `TagParser` | SNBT 词法/语法 | SNBT 字符串解析 |
| `SnbtGrammar` | - | 与 TagParser 同批 |
| `SnbtOperations` | TagParser | SNBT 结构操作 |
| `SnbtPrinterTagVisitor` | - | SNBT 打印（StringTagVisitor 的变体）|
| `StringTagVisitor` | - | Tag → SNBT 字符串 |
| `TextComponentTagVisitor` | Component 系统 | Tag → 聊天组件 |
| `NbtOps` | DataFixers / Codec | Codec SG 专用 |

## 关键修复记录

1. **Float/Double 双重反转**：`BinaryNbtReader.ReadInt()` 已做 `ReverseEndianness`，`NumericTag` 中又做一次导致双重反转。改为直接用 `input.ReadFloat()` / `input.ReadDoubleBits()`。
2. **GZipStream NotSupportedException**：`BinaryNbtReader.SkipBytes` 原用 `BaseStream.Seek`，GZipStream 不支持。改为循环读取并丢弃字节。
3. **PrettyPrintCompound 引号 bug**：key 后误写为空格 `' '`，应为结束引号 `'"'`。
4. **CompareNbt 的 NumericTag Equals**：NumericTag 子类未重写 `Equals`，默认引用比较导致两个 `IntTag(1)` 不等。给所有标量 Tag 重写 `Equals` / `GetHashCode`。
5. **EndTag 三处修正**：`EndTagType.Load` 返回 `EndTag.Instance`、`Parse` 返回 `VisitEnd()`、`Accept` 调用 `VisitEnd`。
6. **NbtAccounter**：`_depth` 改为可变，添加 `PopDepth`。
7. **C# 接口 default method**：不需要 `virtual` 关键字（默认就是 virtual），实现类用 `new` 而非 `override`。
8. **ReportedNbtException CS8862**：主构造函数 + 额外构造函数冲突，改为普通构造函数。

## 接口语义备忘

### StreamTagVisitor.EntryResult 四态
- `Enter`：进入子项，继续访问名字/值
- `Skip`：跳过当前项，继续兄弟
- `Break`：结束当前容器（触发 `VisitContainerEnd`），继续外层
- `Halt`：立即终止整个访问

### StreamTagVisitor.ValueResult 三态
- `Continue`：继续
- `Break`：结束当前容器但继续外层
- `Halt`：立即终止

### CompoundTag 两阶段访问（对应原版 `CompoundTag.Accept`）
1. `VisitEntry(type)` —— 无名字，决定是否进入
2. `VisitEntry(type, name)` —— 有名字，决定是否访问值
