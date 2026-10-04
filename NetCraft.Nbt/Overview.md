# NetCraft.Nbt

> **完成度**：100% · **阶段**：M2 已达成（字节级兼容原版）· **依赖**：NetCraft.Codec · **计划链位置**：Layer 1 序列化底层，PoC 起点之一

Layer 1 子库，NBT（Named Binary Tag）子系统。Minecraft 二进制序列化格式的存档/区块/玩家数据底层。

对应原版 `net.minecraft.nbt` 包，字节级兼容 Minecraft 26.2。

## 实现

- 全部 13 种 Tag 类型（EndTag/ByteTag/.../LongArrayTag）
- 大端字节序读写，Java modified UTF-8 编码
- GZIP 压缩流支持
- 流式访问者（StreamTagVisitor，不构建完整树直接处理原始数据）
- 完整访问者（TagVisitor，遍历已构建的 Tag 对象树）
- NBT 大小统计器防止恶意存档 OOM 与栈溢出
- NbtOps 实现 DynamicOps<Tag> 桥接 Codec 框架
- CompoundTag.store/read 通过 Codec 序列化字段
- TagParser + SnbtGrammar 系列 SNBT 字符串解析支持含 hex/binary int 与 array 语法
- SnbtOperations 提供 SNBT 与 Tag 互转便捷方法
- 24/24 round-trip 兼容性测试通过

## 文件清单（28 个 cs）

### 根目录（22 个 cs，核心 NBT 类型与读写 + SNBT 解析）

| 文件 | 用途 |
|------|------|
| `Tag.cs` | NBT 标签根接口 |
| `TagType.cs` | 标签类型描述（Load/Skip/Parse） |
| `TagTypes.cs` | 按 ID 0-12 索引的类型注册表 |
| `TagVisitor.cs` | 完整 Tag 对象树访问者接口 |
| `StreamTagVisitor.cs` | 流式访问者接口（三态 ValueResult/四态 EntryResult） |
| `NumericTag.cs` | 数值 Tag：EndTag/ByteTag/ShortTag/IntTag/LongTag/FloatTag/DoubleTag |
| `CollectionTags.cs` | StringTag/ByteArrayTag/IntArrayTag/LongArrayTag |
| `CompoundTag.cs` | CompoundTag（TAG_Compound，ID=10，含 store/read Codec 入口） |
| `ListTag.cs` | ListTag（TAG_List，ID=9，同类型 Tag 列表） |
| `NbtOps.cs` | DynamicOps<Tag> 实现桥接 Codec 框架 |
| `INbtReader.cs` | 二进制读取器抽象（大端，Span+BinaryPrimitives） |
| `NbtIo.cs` | 核心读写 API（read/write/compress） |
| `NbtUtils.cs` | 工具方法（比较/PrettyPrint/BlockState 字符串打包） |
| `NbtAccounter.cs` | 大小统计器，跟踪字节数与嵌套深度 |
| `Number.cs` | 数值包装类型（对应 Java `java.lang.Number`） |
| `NbtException.cs` | NBT 通用/格式异常 |
| `ReportedNbtException.cs` | 报告型异常（简化版，待 CrashReport 就绪补全） |
| `TagParser.cs` | SNBT 字符串解析器对应原版 TagParser |
| `SnbtGrammar.cs` | SNBT 文法定义 |
| `SnbtGrammar.CreateParser.cs` | SNBT 文法 parser 构造 |
| `SnbtGrammarTypes.cs` | SNBT 文法类型 |
| `SnbtOperations.cs` | SNBT 与 Tag 互转便捷方法 |

### Visitors 子目录（6 个 cs，字段过滤访问者）

| 文件 | 用途 |
|------|------|
| `Visitors/FieldSelector.cs` | 字段选择器 record，描述要保留的字段（路径+类型+名字） |
| `Visitors/FieldTree.cs` | 字段选择树，按深度组织递归进入的子树 |
| `Visitors/CollectToTag.cs` | 流式访问者基类，将 NBT 流构建为完整 Tag 树 |
| `Visitors/CollectFields.cs` | 收集指定字段的访问者，仅构建选中字段后 BREAK |
| `Visitors/SkipFields.cs` | 跳过指定字段的访问者，其余正常构建 |
| `Visitors/SkipAll.cs` | 跳过所有内容的访问者，不构建任何 Tag |

## 测试

- 已合并到 `NetCraft.Test`（`Modules/NbtTests.cs` 24 个 round-trip + `Modules/SnbtTests.cs` SNBT 解析），详见 `NetCraft.Test/Overview.md`
