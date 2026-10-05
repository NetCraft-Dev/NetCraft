using NetCraft.Codec;
using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Suggestion;
using NetCraft.Game.Commands;
using NetCraft.Game.World.Items;
using NetCraft.Nbt;
using NetCraft.Network.Component;
using NetCraft.Registry;
using NetCraft.Util;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//ItemPredicateArgument 物品谓词参数 对应原版 net.minecraft.commands.arguments.item.ItemPredicateArgument
//语法 <物品> | #<标签> | * 之后可接 [条件,条件...]
//条件之间是且 条件内用 | 表示或 用 ! 取反
//条件三种写法 <组件>=<SNBT值> 值匹配 / <组件> 单独出现是存在性 / <谓词>~<SNBT值>
//原版走 packrat 语法树 本作没有该框架 用等价的递归下降实现 分支顺序与原版一致
public sealed class ItemPredicateArgument : ArgumentType<Predicate<ItemStack>>
{
    //CountId 数量伪组件标识 对应原版 ItemPredicateArgument.COUNT_ID
    //count 不在组件注册表里 单独按数量区间判定
    private static readonly Identifier CountId = Identifier.WithDefaultNamespace("count");

    private static readonly IReadOnlyList<string> ExamplesList =
        new[] { "stick", "minecraft:stick", "#stick", "#stick[foo='bar']" };

    public static readonly DynamicCommandExceptionType ErrorUnknownItem =
        new(id => new LiteralMessage($"未知物品 {id}"));

    public static readonly DynamicCommandExceptionType ErrorUnknownTag =
        new(id => new LiteralMessage($"未知物品标签 {id}"));

    public static readonly DynamicCommandExceptionType ErrorUnknownComponent =
        new(id => new LiteralMessage($"未知组件 {id}"));

    public static readonly Dynamic2CommandExceptionType ErrorMalformedComponent =
        new((type, message) => new LiteralMessage($"组件 {type} 格式错误 {message}"));

    private static RegistryOps<Tag>? _registryOps;
    private static TagParser<Tag>? _componentTagParser;

    //RegistryOpsForCommands 惰性构造 组件值解析时才需要注册表
    private static RegistryOps<Tag> RegistryOpsForCommands
        => _registryOps ??= new RegistryOps<Tag>(NbtOps.Instance, BuiltInRegistries.CreateRegistryAccess());

    private static TagParser<Tag> ComponentTagParser
        => _componentTagParser ??= TagParser<Tag>.Create(RegistryOpsForCommands);

    //ItemPredicate 新建谓词参数实例 命令树注册用
    public static ItemPredicateArgument ItemPredicate() => new();

    public Predicate<ItemStack> Parse(StringReader reader)
    {
        var start = reader.Cursor;
        try
        {
            return ParseTop(reader);
        }
        catch (CommandSyntaxException)
        {
            reader.SetCursor(start);
            throw;
        }
    }

    //ParseTop 基础类型加可选条件列表 对应原版 top 规则
    private static Predicate<ItemStack> ParseTop(StringReader reader)
    {
        var basePredicate = ParseAnyType(reader);
        //没有条件列表时通配与物品标签自身就是完整谓词 对应原版 top 的第二条分支
        if (!reader.CanRead() || reader.Peek() != '[') return basePredicate;
        reader.Skip();
        Predicate<ItemStack>? conditions = null;
        if (reader.CanRead() && reader.Peek() != ']')
            conditions = ParseConditions(reader);
        reader.Expect(']');
        if (conditions is null) return basePredicate;
        //基础类型与条件列表是且的关系 对应原版 Util.allOf
        var rest = conditions;
        return stack => basePredicate(stack) && rest(stack);
    }

    //ParseAnyType 物品标识符 #标签 或 * 通配 对应原版 any_type 规则
    private static Predicate<ItemStack> ParseAnyType(StringReader reader)
    {
        if (!reader.CanRead())
            throw ErrorUnknownItem.CreateWithContext(reader, "");
        //* 匹配任意物品 对应原版 all_type
        if (reader.Peek() == '*')
        {
            reader.Skip();
            return _ => true;
        }
        if (reader.Peek() == '#')
        {
            reader.Skip();
            var tagStart = reader.Cursor;
            var tagId = IdentifierArgument.ReadIdentifier(reader);
            var key = TagKey<Item>.Create(Registries.ITEM, tagId);
            var set = BuiltInRegistries.ITEM.Get(key);
            if (set is null)
            {
                reader.SetCursor(tagStart);
                throw ErrorUnknownTag.CreateWithContext(reader, tagId.ToString());
            }
            return stack => !stack.IsEmpty() && set.Contains(stack.GetTypeHolder()!);
        }
        var itemStart = reader.Cursor;
        var itemId = IdentifierArgument.ReadIdentifier(reader);
        //注册表带默认值 必须按 ResourceKey 查 否则未知物品会静默落到默认项
        var item = BuiltInRegistries.ITEM.GetValue(ResourceKey<Item>.Create(Registries.ITEM, itemId));
        if (item is null)
        {
            reader.SetCursor(itemStart);
            throw ErrorUnknownItem.CreateWithContext(reader, itemId.ToString());
        }
        return stack => !stack.IsEmpty() && ReferenceEquals(stack.GetItem(), item);
    }

    //ParseConditions 逗号分隔的条件全部满足 对应原版 conditions 规则
    private static Predicate<ItemStack> ParseConditions(StringReader reader)
    {
        var first = ParseAlternatives(reader);
        if (!reader.CanRead() || reader.Peek() != ',') return first;
        reader.Skip();
        var rest = ParseConditions(reader);
        return stack => first(stack) && rest(stack);
    }

    //ParseAlternatives 竖线分隔的候选任一满足 对应原版 alternatives 规则
    private static Predicate<ItemStack> ParseAlternatives(StringReader reader)
    {
        var first = ParseTerm(reader);
        if (!reader.CanRead() || reader.Peek() != '|') return first;
        reader.Skip();
        var rest = ParseAlternatives(reader);
        return stack => first(stack) || rest(stack);
    }

    //ParseTerm 取反前缀后接单个条件 对应原版 term 规则 感叹号不可连用
    private static Predicate<ItemStack> ParseTerm(StringReader reader)
    {
        if (reader.CanRead() && reader.Peek() == '!')
        {
            reader.Skip();
            var inner = ParseTest(reader);
            return stack => !inner(stack);
        }
        return ParseTest(reader);
    }

    //ParseTest 组件值匹配 谓词匹配 组件存在性 对应原版 test 规则的三条分支
    //原版三条分支的原子顺序是 component_type '=' tag / predicate_type '~' tag / component_type
    //组件类型解析优先于谓词类型 因为前两条分支都要求组件类型先匹配上
    private static Predicate<ItemStack> ParseTest(StringReader reader)
    {
        var start = reader.Cursor;
        var id = IdentifierArgument.ReadIdentifier(reader);
        //数量伪组件 值是一个整数区间 单独出现时恒真 对应原版 PSEUDO_COMPONENTS 的 count
        if (id == CountId)
        {
            if (reader.CanRead() && reader.Peek() == '=')
            {
                reader.Skip();
                var range = ReadCountRange(reader, ReadNbt(reader));
                return stack => !stack.IsEmpty() && range.Matches(stack.GetCount());
            }
            return _ => true;
        }
        if (LookupComponentType(id) is { } componentType)
        {
            if (reader.CanRead() && reader.Peek() == '=')
            {
                reader.Skip();
                var expected = ReadComponentValue(reader, componentType, start);
                //值匹配的语义就是精确谓词 对应原版 DataComponentExactPredicate.expect(type, value)
                var exact = DataComponentExactPredicate.Expect(componentType, expected);
                return stack => !stack.IsEmpty() && exact.Test(stack.GetComponents());
            }
            return stack => !stack.IsEmpty() && stack.GetComponents().Get(componentType) is not null;
        }
        //谓词分支 本作没有数据组件谓词注册表 只能按原版 lookupPredicateType 的 or 链回退到组件存在性
        if (LookupPredicateType(id) is { } predicateType)
        {
            if (reader.CanRead() && reader.Peek() == '~')
            {
                reader.Skip();
                ReadNbt(reader);
                return predicateType;
            }
        }
        reader.SetCursor(start);
        throw ErrorUnknownComponent.CreateWithContext(reader, id.ToString());
    }

    //LookupComponentType 按标识符查持久化组件类型 非持久化组件按原版报未知
    private static DataComponentType<object>? LookupComponentType(Identifier id)
        => BuiltInRegistries.DATA_COMPONENT_TYPE.GetValue(id) is DataComponentType<object> type && !type.IsTransient
            ? type
            : null;

    //LookupPredicateType 按标识符查谓词 注册表为空时回退到同名组件的存在性检查 对应原版 or 链
    private static Predicate<ItemStack>? LookupPredicateType(Identifier id)
    {
        if (BuiltInRegistries.DATA_COMPONENT_PREDICATE_TYPE.GetValue(id) is not null)
            return null;
        return LookupComponentType(id) is { } componentType
            ? stack => !stack.IsEmpty() && stack.GetComponents().Get(componentType) is not null
            : null;
    }

    //ReadComponentValue 组件值先按 SNBT 读成 Tag 再交给组件 Codec 解析
    private static object ReadComponentValue(StringReader reader, DataComponentType<object> type, int errorStart)
    {
        var tag = ReadNbt(reader);
        var parsed = type.CodecOrThrow().Parse(RegistryOpsForCommands, tag);
        return parsed.MapOrElse<object>(
            value => value,
            message =>
            {
                reader.SetCursor(errorStart);
                throw ErrorMalformedComponent.CreateWithContext(reader, type.ToString()!, message);
            });
    }

    //ReadCountRange 数量伪组件的值 单个整数视为上下界相等 复合标签读 min/max
    private static MinMaxBounds.Ints ReadCountRange(StringReader reader, Tag tag)
    {
        if (tag is IntTag intTag)
            return new MinMaxBounds.Ints(intTag.Value, intTag.Value);
        if (tag is CompoundTag compound && (compound.Contains("min") || compound.Contains("max")))
            return new MinMaxBounds.Ints(compound.GetInt("min")?.Value, compound.GetInt("max")?.Value);
        throw ErrorMalformedComponent.CreateWithContext(reader, CountId.ToString(), "应为整数或 min/max 区间");
    }

    //ReadNbt 用独立读取器解析 SNBT 解析后把游标同步回命令读取器
    private static Tag ReadNbt(StringReader reader)
    {
        var nbtReader = new CommandStringReader(reader.String) { Cursor = reader.Cursor };
        var tag = ComponentTagParser.ParseAsArgument(nbtReader);
        reader.SetCursor(nbtReader.Cursor);
        return tag;
    }

    //GetItemPredicate 取解析出的谓词
    public static Predicate<ItemStack> GetItemPredicate(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<Predicate<ItemStack>>(name);

    //ListSuggestions 按当前游标位置给物品或标签候选 对应原版 ResourceLookupRule 的建议
    public Task<Suggestions> ListSuggestions<S>(CommandContext<S> context, SuggestionsBuilder builder)
    {
        var remaining = builder.Remaining;
        if (remaining.StartsWith('#'))
        {
            foreach (var set in BuiltInRegistries.ITEM.GetTags())
            {
                var text = "#" + set.Key.Location.ToShortString();
                if (text.StartsWith(remaining, StringComparison.Ordinal))
                    builder.Add(text);
            }
        }
        else
        {
            foreach (var id in BuiltInRegistries.ITEM.KeySet)
            {
                var text = id.ToShortString();
                if (text.StartsWith(remaining, StringComparison.Ordinal))
                    builder.Add(text);
            }
            //标签与通配也作为候选给出 对应原版 listTagTypes 与 all_type
            if ("#".StartsWith(remaining, StringComparison.Ordinal)) builder.Add("#");
            if ("*".StartsWith(remaining, StringComparison.Ordinal)) builder.Add("*");
        }
        return builder.BuildFuture();
    }

    public IReadOnlyList<string> Examples => ExamplesList;
}
