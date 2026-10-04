# NetCraft.Commands

> **完成度**：100% · **阶段**：brigadier 完整移植 · **依赖**：NetCraft.Codec · **计划链位置**：Layer 3 命令框架，对应原版 commands/ (110 文件) 框架部分

Layer 3 子库，命令框架。完整移植 Mojang brigadier 库，提供命令的解析、注册、执行、补全、重定向、fork 等能力。

对应原版 `com.mojang.brigadier` + `net.minecraft.commands` 包。

## 实现

brigadier 完整移植完成，47 个 Java 源文件 1:1 对齐到 49 个 C# 文件（合并部分嵌套类型），覆盖 7 个子命名空间。

源仓库参考 `d:/Programming/C#/NetCraft/brigadier/`（已克隆，仅作参考不入库）。

## 与 Util Packrat 的关系

`NetCraft.Util/Parsing/Packrat/Commands/` 下有独立的 `CommandStringReader` + `CommandSyntaxException` + `SimpleCommandExceptionType` + `DynamicCommandExceptionType`，是 Packrat parser 框架私有组件，被 `NetCraft.Nbt/SnbtGrammar.cs` + `TagParser.cs` 使用。两套互不依赖，命名空间隔离（`NetCraft.Commands.Exceptions` vs `NetCraft.Util.Parsing.Packrat.Commands`）。

brigadier 移植必须独立放在 NetCraft.Commands，不动 Util Packrat。

## 文件清单（49 个 cs）

### 根目录 NetCraft.Commands（12 个）
- `Message.cs` / `LiteralMessage.cs` — 消息接口与字面实现
- `ImmutableStringReader.cs` / `StringReader.cs` — 只读接口与游标式读取器
- `Command.cs` — 命令回调委托 + SingleSuccess 常量
- `RedirectModifier.cs` / `SingleRedirectModifier.cs` — 重定向修改器委托
- `ResultConsumer.cs` / `AmbiguityConsumer.cs` — 结果与歧义消费者委托
- `CommandDispatcher.cs` — 命令分发器（parseNodes 递归 + execute + 补全 + 路径查找）
- `ParseResults.cs` — 解析结果
- `CommandSourceStack.cs` — MC 层命令源基类（保留扩展）

### Arguments/（8 个，namespace NetCraft.Commands.Arguments）
- `ArgumentType.cs` — 参数类型接口（含默认 ListSuggestions/Examples）
- `BoolArgumentType.cs` / `IntegerArgumentType.cs` / `LongArgumentType.cs`
- `FloatArgumentType.cs` / `DoubleArgumentType.cs`
- `StringType.cs` — 字符串类型三静态实例（C# enum 不能持 String[] 拆出）
- `StringArgumentType.cs` — 含 EscapeIfRequired/Escape 静态方法

### Builder/（3 个，namespace NetCraft.Commands.Builder）
- `ArgumentBuilder.cs` — 抽象基类 + 自递归泛型 `ArgumentBuilder<S, T> where T : ArgumentBuilder<S, T>`
- `LiteralArgumentBuilder.cs` / `RequiredArgumentBuilder.cs`

### Context/（7 个，namespace NetCraft.Commands.Context）
- `StringRange.cs` — sealed record
- `ParsedArgument.cs` — 非泛型基类 + 泛型派生（对应 `ParsedArgument<S, ?>`）
- `ParsedCommandNode.cs` — sealed record
- `SuggestionContext.cs` — sealed record
- `CommandContext.cs` — 含 PRIMITIVE_TO_WRAPPER 字典、GetArgument、CopyFor
- `CommandContextBuilder.cs` — 累积解析中间状态、FindSuggestionContext
- `ContextChain.cs` — TryFlatten 展平 + ExecuteAll forkedMode 累积 + Stage 枚举

### Exceptions/（10 个，namespace NetCraft.Commands.Exceptions）
- `CommandExceptionType.cs` — 标记接口 ICommandExceptionType
- `CommandSyntaxException.cs` — 含静态 BuiltInExceptions/EnableCommandStackTraces/ContextAmount
- `BuiltInExceptionProvider.cs` / `BuiltInExceptions.cs` — 25 个标准异常类型工厂
- `SimpleCommandExceptionType.cs` / `DynamicCommandExceptionType.cs`
- `Dynamic2/3/4/NCommandExceptionType.cs`

### Suggestion/（5 个，namespace NetCraft.Commands.Suggestion）
- `Suggestion.cs` — IComparable + IEquatable + Apply/Expand
- `IntegerSuggestion.cs`
- `Suggestions.cs` — 静态 Empty/Merge/Create 返回 Task<Suggestions>
- `SuggestionsBuilder.cs`
- `SuggestionProvider.cs` — delegate `Task<Suggestions> SuggestionProvider<S>(CommandContext<S>, SuggestionsBuilder)`

### Tree/（4 个，namespace NetCraft.Commands.Tree）
- `CommandNode.cs` — abstract + IComparable，三索引 children/literals/arguments
- `RootCommandNode.cs` / `LiteralCommandNode.cs`
- `ArgumentCommandNode.cs` — 拆非泛型基类 + 泛型派生（对应 `ArgumentCommandNode<S, ?>`）

## 关键设计决策

- **StringReader 独立新建**：brigadier 的 StringReader 抛 NetCraft.Commands.Exceptions.CommandSyntaxException（含 type/context/input/cursor），与 Util Packrat 的 CommandStringReader 类型签名不同，无法共享
- **CompletableFuture → Task<T>**：`completedFuture(x)` → `Task.FromResult(x)`，异步方法用 async/await + Task.WhenAll
- **ArgumentCommandNode<S, ?> 通配符**：拆双层（非泛型基类持 Name/CustomSuggestions + 泛型派生持强类型 ArgumentType<T>），CommandNode.arguments 索引存非泛型基类
- **ParsedArgument<S, ?>**：同样拆双层，CommandContext.arguments 存非泛型基类，GetArgument 用 IsAssignableFrom 校验
- **ArgumentBuilder<S, T> 自递归泛型**：`where T : ArgumentBuilder<S, T>` 保证链式方法返回 T
- **Optional<T> 复用 NetCraft.Codec.Optional<T>**：ContextChain.TryFlatten 返回 Optional<ContextChain<S>>
- **Command 接口 → delegate**：`delegate int Command<S>(CommandContext<S>)` + SingleSuccess 常量
- **CommandSyntaxException 继承 System.Exception**：checked → unchecked，静态字段 BuiltInExceptions 可运行时替换

## 验证

- `dotnet build NetCraft.slnx` — 0 编译错误
- `dotnet run -- commands` — 32/32 测试通过（StringReader 11 + 异常 3 + 注册解析 5 + 执行 3 + 错误路径 3 + Suggestion 3 + Redirect/Fork 2 + 路径 1 + 歧义 1）
