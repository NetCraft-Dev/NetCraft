using NetCraft.Codec;
using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Suggestion;
using NetCraft.Game.Commands;
using NetCraft.Nbt;
using NetCraft.Network.Component;
using NetCraft.Registry;
using NetCraft.Util;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//ItemInput 物品参数解析结果 对应原版 ItemInput
//带物品本体 标识符与组件补丁 标识符用于回执显示名字
public sealed record ItemInput(Item Item, Identifier Id, DataComponentPatch Components);

//ItemArgument 物品参数对应原版 net.minecraft.commands.arguments.item.ItemArgument
//语法 <物品标识符>[<组件>=<SNBT值>,!<组件>,...] 组件值先走 SNBT 再由组件自身的持久化 Codec 解析
//网络 id 走 item_stack 与原版 give 的 item 参数类型一致
public sealed class ItemArgument : ArgumentType<ItemInput>
{
    private const char SyntaxStartComponents = '[';
    private const char SyntaxEndComponents = ']';
    private const char SyntaxComponentSeparator = ',';
    private const char SyntaxComponentAssignment = '=';
    private const char SyntaxRemovedComponent = '!';

    private static readonly IReadOnlyList<string> ExamplesList =
        new[] { "stick", "minecraft:stick", "stick[damage=1]" };

    public static readonly DynamicCommandExceptionType ErrorUnknownItem =
        new(name => new LiteralMessage($"未知物品 {name}"));

    public static readonly DynamicCommandExceptionType ErrorUnknownComponent =
        new(name => new LiteralMessage($"未知组件 {name}"));

    public static readonly Dynamic2CommandExceptionType ErrorMalformedComponent =
        new((type, message) => new LiteralMessage($"组件 {type} 格式错误 {message}"));

    public static readonly SimpleCommandExceptionType ErrorExpectedComponent =
        new(new LiteralMessage("缺少组件"));

    public static readonly DynamicCommandExceptionType ErrorRepeatedComponent =
        new(name => new LiteralMessage($"组件重复 {name}"));

    private static RegistryOps<Tag>? _registryOps;
    private static TagParser<Tag>? _componentTagParser;

    //RegistryOpsForCommands 惰性构造 组件值解析时才需要注册表
    private static RegistryOps<Tag> RegistryOpsForCommands
        => _registryOps ??= new RegistryOps<Tag>(NbtOps.Instance, BuiltInRegistries.CreateRegistryAccess());

    private static TagParser<Tag> ComponentTagParser
        => _componentTagParser ??= TagParser<Tag>.Create(RegistryOpsForCommands);

    public static ItemArgument Item() => new();

    //ParseState 解析过程状态 补全路径只关心 Suggest 组件映射照常填充但不使用
    private sealed class ParseState
    {
        public readonly Dictionary<object, Optional<object>> Components = new();
        public readonly HashSet<object> Seen = new(ReferenceEqualityComparer.Instance);

        //Suggest 当前游标处该给的补全 null 表示不给
        public Action<SuggestionsBuilder>? Suggest;
    }

    public ItemInput Parse(StringReader reader)
    {
        var start = reader.Cursor;
        try
        {
            return ParseInternal(reader, new ParseState());
        }
        catch (CommandSyntaxException)
        {
            reader.SetCursor(start);
            throw;
        }
    }

    //ParseInternal 物品与组件共用一条解析流程 补全时靠 State.Suggest 收集当前应给的候选
    private static ItemInput ParseInternal(StringReader reader, ParseState state)
    {
        state.Suggest = SuggestItem;
        var start = reader.Cursor;
        var id = IdentifierArgument.ReadIdentifier(reader);
        //按 ResourceKey 查表 物品注册表带默认值 按 Identifier 查未知物品会落到默认项
        var item = BuiltInRegistries.ITEM.GetValue(ResourceKey<Item>.Create(Registries.ITEM, id));
        if (item is null)
        {
            reader.SetCursor(start);
            throw ErrorUnknownItem.CreateWithContext(reader, id.ToString());
        }
        state.Suggest = SuggestStartComponents;
        if (reader.CanRead() && reader.Peek() == SyntaxStartComponents)
        {
            state.Suggest = null;
            ParseComponents(reader, state);
        }
        //解析成功不重置建议 物品位之后仍可补 [ 对应原版 parse 末尾不改 suggestions
        return new ItemInput(item, id, new DataComponentPatch(state.Components));
    }

    //ParseComponents 解析 [组件=值,!组件,...] 对齐原版 ItemParser.State.readComponents
    private static void ParseComponents(StringReader reader, ParseState state)
    {
        reader.Expect(SyntaxStartComponents);
        state.Suggest = SuggestComponentAssignmentOrRemoval;
        while (reader.CanRead() && reader.Peek() != SyntaxEndComponents)
        {
            reader.SkipWhitespace();
            if (reader.CanRead() && reader.Peek() == SyntaxRemovedComponent)
            {
                reader.Skip();
                state.Suggest = builder => SuggestComponent(builder, string.Empty);
                var removed = ReadComponentType(reader);
                if (!state.Seen.Add(removed))
                    throw ErrorRepeatedComponent.Create(removed.ToString()!);
                state.Components[removed] = Optional<object>.Empty();
                state.Suggest = null;
                reader.SkipWhitespace();
            }
            else
            {
                var type = ReadComponentType(reader);
                if (!state.Seen.Add(type))
                    throw ErrorRepeatedComponent.Create(type.ToString()!);
                state.Suggest = SuggestAssignment;
                reader.SkipWhitespace();
                reader.Expect(SyntaxComponentAssignment);
                state.Suggest = null;
                reader.SkipWhitespace();
                state.Components[type] = Optional<object>.Of(ReadComponentValue(reader, type));
                reader.SkipWhitespace();
            }
            state.Suggest = SuggestNextOrEndComponents;
            if (!reader.CanRead() || reader.Peek() != SyntaxComponentSeparator)
                break;
            reader.Skip();
            reader.SkipWhitespace();
            state.Suggest = SuggestComponentAssignmentOrRemoval;
            if (!reader.CanRead())
                throw ErrorExpectedComponent.CreateWithContext(reader);
        }
        reader.Expect(SyntaxEndComponents);
        state.Suggest = null;
    }

    //ReadComponentType 读组件标识符并查注册表 非持久化组件按原版报未知组件
    private static DataComponentType<object> ReadComponentType(StringReader reader)
    {
        if (!reader.CanRead())
            throw ErrorExpectedComponent.CreateWithContext(reader);
        var start = reader.Cursor;
        var id = IdentifierArgument.ReadIdentifier(reader);
        if (BuiltInRegistries.DATA_COMPONENT_TYPE.GetValue(id) is not DataComponentType<object> type
            || type.IsTransient)
        {
            reader.SetCursor(start);
            throw ErrorUnknownComponent.CreateWithContext(reader, id.ToString());
        }
        return type;
    }

    //ReadComponentValue 组件值先按 SNBT 读成 Tag 再交给组件 Codec 解析
    private static object ReadComponentValue(StringReader reader, DataComponentType<object> type)
    {
        var start = reader.Cursor;
        //SNBT解析器用独立读取器 解析后同步游标
        var nbtReader = new CommandStringReader(reader.String) { Cursor = reader.Cursor };
        var tag = ComponentTagParser.ParseAsArgument(nbtReader);
        reader.SetCursor(nbtReader.Cursor);
        var parsed = type.CodecOrThrow().Parse(RegistryOpsForCommands, tag);
        return parsed.MapOrElse<object>(
            value => value,
            message =>
            {
                reader.SetCursor(start);
                throw ErrorMalformedComponent.CreateWithContext(reader, type.ToString()!, message);
            });
    }

    //GetItemInput 取解析出的物品与标识符
    public static ItemInput GetItemInput(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<ItemInput>(name);

    //GetItem 取解析出的物品
    public static Item GetItem(CommandContext<CommandSourceStack> context, string name)
        => GetItemInput(context, name).Item;

    //ListSuggestions 走同一条解析流程 中断处按当前语法位置给候选 对齐原版 ItemParser.fillSuggestions
    public Task<Suggestions> ListSuggestions<S>(CommandContext<S> context, SuggestionsBuilder builder)
    {
        var reader = new StringReader(builder.Input);
        reader.SetCursor(builder.Start);
        var state = new ParseState();
        try
        {
            ParseInternal(reader, state);
        }
        catch (CommandSyntaxException)
        {
        }
        //建议基于当前游标偏移 返回偏移构建器的结果对应原版 resolveSuggestions
        var offset = builder.CreateOffset(reader.Cursor);
        state.Suggest?.Invoke(offset);
        return offset.BuildFuture();
    }

    //SuggestItem 补全注册表内全部物品标识符 默认命名空间省略前缀
    private static void SuggestItem(SuggestionsBuilder builder)
    {
        foreach (var id in BuiltInRegistries.ITEM.KeySet)
        {
            var text = id.ToShortString();
            if (text.StartsWith(builder.RemainingLowerCase, StringComparison.Ordinal))
                builder.Add(text);
        }
    }

    //SuggestStartComponents 物品标识符之后只剩组件列表可写
    private static void SuggestStartComponents(SuggestionsBuilder builder)
    {
        if (builder.Remaining.Length == 0)
            builder.Add(SyntaxStartComponents.ToString());
    }

    //SuggestComponentAssignmentOrRemoval 组件位可写 ! 前缀移除 也可写组件标识符
    private static void SuggestComponentAssignmentOrRemoval(SuggestionsBuilder builder)
    {
        builder.Add(SyntaxRemovedComponent.ToString());
        SuggestComponent(builder, SyntaxComponentAssignment.ToString());
    }

    //SuggestComponent 列出有持久化 Codec 的组件标识符 可带后缀
    private static void SuggestComponent(SuggestionsBuilder builder, string suffix)
    {
        var contents = builder.Remaining.ToLowerInvariant();
        foreach (var id in BuiltInRegistries.DATA_COMPONENT_TYPE.KeySet)
        {
            if (BuiltInRegistries.DATA_COMPONENT_TYPE.GetValue(id) is not DataComponentType<object> type
                || type.Codec is null)
                continue;
            var text = id.ToString();
            if (text.StartsWith(contents, StringComparison.Ordinal))
                builder.Add(text + suffix);
        }
    }

    private static void SuggestAssignment(SuggestionsBuilder builder)
    {
        if (builder.Remaining.Length == 0)
            builder.Add(SyntaxComponentAssignment.ToString());
    }

    private static void SuggestNextOrEndComponents(SuggestionsBuilder builder)
    {
        if (builder.Remaining.Length == 0)
        {
            builder.Add(SyntaxComponentSeparator.ToString());
            builder.Add(SyntaxEndComponents.ToString());
        }
    }

    public IReadOnlyList<string> Examples => ExamplesList;
}
