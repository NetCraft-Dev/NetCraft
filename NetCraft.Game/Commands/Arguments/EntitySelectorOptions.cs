using NetCraft.Commands;
using NetCraft.Commands.Exceptions;
using NetCraft.Game.World.Entity;
using NetCraft.Game.World.Level;
using NetCraft.Nbt;
using NetCraft.Registry;
using NetCraft.Util;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//EntitySelectorOptions 选择器选项注册表对应原版 net.minecraft.commands.arguments.selector.options.EntitySelectorOptions
//17个[name=value]选项name/gamemode/type/team可反转其余按各自语法解析
//team/tag/nbt/scores/advancements/predicate按玩家模型降级 team无队伍tag无标签nbt无序列化数据scores无计分板advancements无成就predicate无战利品谓词
public static class EntitySelectorOptions
{
    //Modifier 选项处理器消费选项值改写解析器状态
    public delegate void Modifier(EntitySelectorParser parser);

    //Option 单个选项 处理器与适用性谓词
    private sealed record Option(Modifier Handler, Predicate<EntitySelectorParser> CanUse);

    public static readonly DynamicCommandExceptionType ErrorUnknownOption =
        new(name => new TranslatableMessage("argument.entity.options.unknown", name));
    public static readonly DynamicCommandExceptionType ErrorInapplicableOption =
        new(name => new TranslatableMessage("argument.entity.options.inapplicable", name));
    public static readonly SimpleCommandExceptionType ErrorRangeNegative =
        new(new TranslatableMessage("argument.entity.options.distance.negative"));
    public static readonly SimpleCommandExceptionType ErrorLevelNegative =
        new(new TranslatableMessage("argument.entity.options.level.negative"));
    public static readonly SimpleCommandExceptionType ErrorLimitTooSmall =
        new(new TranslatableMessage("argument.entity.options.limit.toosmall"));
    public static readonly DynamicCommandExceptionType ErrorSortUnknown =
        new(name => new TranslatableMessage("argument.entity.options.sort.irreversible", name));
    public static readonly DynamicCommandExceptionType ErrorGameModeInvalid =
        new(name => new TranslatableMessage("argument.entity.options.mode.invalid", name));
    public static readonly DynamicCommandExceptionType ErrorEntityTypeInvalid =
        new(type => new TranslatableMessage("argument.entity.options.type.invalid", type));

    //Options 全部选项表 原版bootStrap注册顺序
    private static readonly Dictionary<string, Option> Options = BuildOptions();

    private static Dictionary<string, Option> BuildOptions()
    {
        var options = new Dictionary<string, Option>
        {
            ["name"] = new(HandleName, p => p.NameOption.CanParseAny),
            ["distance"] = new(HandleDistance, p => p.Distance is null),
            ["level"] = new(HandleLevel, p => p.Level is null),
            ["x"] = new(HandleX, p => p.X is null),
            ["y"] = new(HandleY, p => p.Y is null),
            ["z"] = new(HandleZ, p => p.Z is null),
            ["dx"] = new(HandleDx, p => p.DeltaX is null),
            ["dy"] = new(HandleDy, p => p.DeltaY is null),
            ["dz"] = new(HandleDz, p => p.DeltaZ is null),
            ["x_rotation"] = new(HandleXRotation, p => p.RotX is null),
            ["y_rotation"] = new(HandleYRotation, p => p.RotY is null),
            ["limit"] = new(HandleLimit, p => !p.IsCurrentEntity && p.LimitedOption.CanParse),
            ["sort"] = new(HandleSort, p => !p.IsCurrentEntity && p.SortedOption.CanParse),
            ["gamemode"] = new(HandleGamemode, p => p.GamemodeOption.CanParseAny),
            ["team"] = new(HandleTeam, p => p.TeamOption.CanParseAny),
            ["type"] = new(HandleType, p => p.TypeOption.CanParseAny),
            ["tag"] = new(HandleTag, _ => true),
            ["nbt"] = new(HandleNbt, _ => true),
            ["scores"] = new(HandleScores, p => p.ScoresOption.CanParse),
            ["advancements"] = new(HandleAdvancements, p => p.AdvancementsOption.CanParse),
            ["predicate"] = new(HandlePredicate, _ => true),
        };
        return options;
    }

    //Get 按名取选项处理器 未知或当前选择器不可用回滚游标抛异常
    public static Modifier Get(EntitySelectorParser parser, string key, int start)
    {
        if (Options.TryGetValue(key, out var option))
        {
            if (option.CanUse(parser))
                return option.Handler;
            throw RollbackAndThrow(parser, start, ErrorInapplicableOption, key);
        }
        throw RollbackAndThrow(parser, start, ErrorUnknownOption, key);
    }

    //RollbackAndThrow 回滚游标到选项起点再构造异常
    private static CommandSyntaxException RollbackAndThrow(EntitySelectorParser parser, int start,
        SimpleCommandExceptionType type)
    {
        parser.Reader.SetCursor(start);
        return type.CreateWithContext(parser.Reader);
    }

    private static CommandSyntaxException RollbackAndThrow(EntitySelectorParser parser, int start,
        DynamicCommandExceptionType type, string argument)
    {
        parser.Reader.SetCursor(start);
        return type.CreateWithContext(parser.Reader, argument);
    }

    //HandleName 玩家名过滤支持引号与反转
    private static void HandleName(EntitySelectorParser parser)
    {
        var start = parser.Reader.Cursor;
        var inverted = parser.ShouldInvertValue();
        var name = parser.Reader.ReadString();
        var state = parser.NameOption;
        if (!state.CanParseElement(inverted))
            throw RollbackAndThrow(parser, start, ErrorInapplicableOption, "name");
        state.MarkParsedElement(inverted);
        parser.AddPredicate(e => (e.Name == name) != inverted);
    }

    //HandleDistance 距离区间负值非法
    private static void HandleDistance(EntitySelectorParser parser)
    {
        var start = parser.Reader.Cursor;
        var value = MinMaxBounds.Doubles.FromReader(parser.Reader);
        if ((value.Min is not null && value.Min < 0.0) || (value.Max is not null && value.Max < 0.0))
            throw RollbackAndThrow(parser, start, ErrorRangeNegative);
        parser.SetDistance(value);
        parser.SetWorldLimited();
    }

    //HandleLevel 经验等级区间负值非法 仅玩家有等级
    private static void HandleLevel(EntitySelectorParser parser)
    {
        var start = parser.Reader.Cursor;
        var value = MinMaxBounds.Ints.FromReader(parser.Reader);
        if ((value.Min is not null && value.Min < 0) || (value.Max is not null && value.Max < 0))
            throw RollbackAndThrow(parser, start, ErrorLevelNegative);
        parser.SetLevel(value);
        parser.SetIncludesEntities(false);
    }

    private static void HandleX(EntitySelectorParser parser)
    {
        parser.SetWorldLimited();
        parser.SetX(parser.Reader.ReadDouble());
    }

    private static void HandleY(EntitySelectorParser parser)
    {
        parser.SetWorldLimited();
        parser.SetY(parser.Reader.ReadDouble());
    }

    private static void HandleZ(EntitySelectorParser parser)
    {
        parser.SetWorldLimited();
        parser.SetZ(parser.Reader.ReadDouble());
    }

    private static void HandleDx(EntitySelectorParser parser)
    {
        parser.SetWorldLimited();
        parser.SetDeltaX(parser.Reader.ReadDouble());
    }

    private static void HandleDy(EntitySelectorParser parser)
    {
        parser.SetWorldLimited();
        parser.SetDeltaY(parser.Reader.ReadDouble());
    }

    private static void HandleDz(EntitySelectorParser parser)
    {
        parser.SetWorldLimited();
        parser.SetDeltaZ(parser.Reader.ReadDouble());
    }

    private static void HandleXRotation(EntitySelectorParser parser)
        => parser.SetRotX(MinMaxBounds.FloatDegrees.FromReader(parser.Reader));

    private static void HandleYRotation(EntitySelectorParser parser)
        => parser.SetRotY(MinMaxBounds.FloatDegrees.FromReader(parser.Reader));

    //HandleLimit 结果上限至少1
    private static void HandleLimit(EntitySelectorParser parser)
    {
        var start = parser.Reader.Cursor;
        var count = parser.Reader.ReadInt();
        if (count < 1)
            throw RollbackAndThrow(parser, start, ErrorLimitTooSmall);
        parser.SetMaxResults(count);
        parser.LimitedOption.MarkParsed();
    }

    //HandleSort 排序策略四值
    private static void HandleSort(EntitySelectorParser parser)
    {
        var start = parser.Reader.Cursor;
        var name = parser.Reader.ReadUnquotedString();
        EntitySelector.Orderer order = name switch
        {
            "nearest" => EntitySelectorParser.OrderNearest,
            "furthest" => EntitySelectorParser.OrderFurthest,
            "random" => EntitySelectorParser.OrderRandom,
            "arbitrary" => EntitySelector.OrderArbitrary,
            _ => throw RollbackAndThrow(parser, start, ErrorSortUnknown, name),
        };
        parser.SetOrder(order);
        parser.SortedOption.MarkParsed();
    }

    //HandleGamemode 游戏模式过滤 反转语义为不等于该模式
    private static void HandleGamemode(EntitySelectorParser parser)
    {
        var start = parser.Reader.Cursor;
        var state = parser.GamemodeOption;
        var inverted = parser.ShouldInvertValue();
        if (!state.CanParseElement(inverted))
            throw RollbackAndThrow(parser, start, ErrorInapplicableOption, "gamemode");
        var name = parser.Reader.ReadUnquotedString();
        var expected = GameType.ByName(name);
        if (expected is null)
            throw RollbackAndThrow(parser, start, ErrorGameModeInvalid, name);
        parser.SetIncludesEntities(false);
        parser.AddPredicate(e => (e.GameType == expected) != inverted);
        state.MarkParsedElement(inverted);
    }

    //HandleTeam 队伍过滤 本作无队伍系统 玩家恒空队名
    private static void HandleTeam(EntitySelectorParser parser)
    {
        var start = parser.Reader.Cursor;
        var state = parser.TeamOption;
        var inverted = parser.ShouldInvertValue();
        var expected = parser.Reader.ReadUnquotedString();
        if (!state.CanParseElement(inverted))
            throw RollbackAndThrow(parser, start, ErrorInapplicableOption, "team");
        parser.AddPredicate(e => (string.Empty == expected) != inverted);
        state.MarkParsedElement(inverted);
    }

    //HandleType 实体类型过滤 #前缀走类型标签 玩家类型正向取值时排除其他实体语义
    private static void HandleType(EntitySelectorParser parser)
    {
        var start = parser.Reader.Cursor;
        var state = parser.TypeOption;
        var inverted = parser.ShouldInvertValue();
        if (parser.IsTag())
        {
            if (!state.CanParseAnyTag)
                throw RollbackAndThrow(parser, start, ErrorInapplicableOption, "type");
            var tagId = IdentifierArgument.ReadIdentifier(parser.Reader);
            if (!state.CanParseTag(tagId))
                throw RollbackAndThrow(parser, start, ErrorInapplicableOption, "type");
            //本作无实体类型标签 玩家恒不命中 反转后恒通过
            parser.AddPredicate(_ => inverted);
            state.MarkParsedTag(tagId);
            return;
        }
        if (!state.CanParseElement(inverted))
            throw RollbackAndThrow(parser, start, ErrorInapplicableOption, "type");
        var id = IdentifierArgument.ReadIdentifier(parser.Reader);
        var type = BuiltInRegistries.ENTITY_TYPE.GetValue(id);
        if (type is null)
            throw RollbackAndThrow(parser, start, ErrorEntityTypeInvalid, id.ToString());
        if (ReferenceEquals(type, EntityTypes.PLAYER) && !inverted)
            parser.SetIncludesEntities(false);
        parser.AddPredicate(e => (ReferenceEquals(e.Type, type)) != inverted);
        if (!inverted)
            parser.LimitToType(type);
        state.MarkParsedElement(inverted);
    }

    //HandleTag 实体标签过滤 玩家恒无标签 空值表示无标签者
    private static void HandleTag(EntitySelectorParser parser)
    {
        var inverted = parser.ShouldInvertValue();
        var tag = parser.Reader.ReadUnquotedString();
        parser.AddPredicate(_ => (tag.Length == 0) != inverted);
    }

    //HandleNbt NBT过滤 SNBT语法照原版解析 玩家无序列化NBT恒不匹配 反转后恒通过
    private static void HandleNbt(EntitySelectorParser parser)
    {
        var inverted = parser.ShouldInvertValue();
        //SNBT解析器用独立读取器 解析后同步游标
        var nbtReader = new CommandStringReader(parser.Reader.String) { Cursor = parser.Reader.Cursor };
        TagParser<object>.ParseCompoundAsArgument(nbtReader);
        parser.Reader.SetCursor(nbtReader.Cursor);
        parser.AddPredicate(_ => inverted);
    }

    //HandleScores 计分板过滤 {目标名=区间,...}语法照原版解析 本作无计分板条件恒不通过
    private static void HandleScores(EntitySelectorParser parser)
    {
        var reader = parser.Reader;
        reader.Expect('{');
        reader.SkipWhitespace();
        while (reader.CanRead() && reader.Peek() != '}')
        {
            reader.SkipWhitespace();
            reader.ReadUnquotedString();
            reader.SkipWhitespace();
            reader.Expect('=');
            reader.SkipWhitespace();
            MinMaxBounds.Ints.FromReader(reader);
            reader.SkipWhitespace();
            if (reader.CanRead() && reader.Peek() == ',')
                reader.Skip();
        }
        reader.Expect('}');
        parser.AddPredicate(_ => false);
        parser.ScoresOption.MarkParsed();
    }

    //HandleAdvancements 成就过滤 {成就id=布尔或{条件=布尔},...}语法照原版解析 本作无成就条件恒不通过
    private static void HandleAdvancements(EntitySelectorParser parser)
    {
        var reader = parser.Reader;
        reader.Expect('{');
        reader.SkipWhitespace();
        while (reader.CanRead() && reader.Peek() != '}')
        {
            reader.SkipWhitespace();
            IdentifierArgument.ReadIdentifier(reader);
            reader.SkipWhitespace();
            reader.Expect('=');
            reader.SkipWhitespace();
            if (reader.CanRead() && reader.Peek() == '{')
            {
                reader.SkipWhitespace();
                reader.Expect('{');
                reader.SkipWhitespace();
                while (reader.CanRead() && reader.Peek() != '}')
                {
                    reader.SkipWhitespace();
                    reader.ReadUnquotedString();
                    reader.SkipWhitespace();
                    reader.Expect('=');
                    reader.SkipWhitespace();
                    reader.ReadBoolean();
                    reader.SkipWhitespace();
                    if (reader.CanRead() && reader.Peek() == ',')
                        reader.Skip();
                }
                reader.SkipWhitespace();
                reader.Expect('}');
                reader.SkipWhitespace();
            }
            else
            {
                reader.ReadBoolean();
            }
            reader.SkipWhitespace();
            if (reader.CanRead() && reader.Peek() == ',')
                reader.Skip();
        }
        reader.Expect('}');
        parser.SetIncludesEntities(false);
        parser.AddPredicate(_ => false);
        parser.AdvancementsOption.MarkParsed();
    }

    //HandlePredicate 战利品谓词过滤 本作无谓词库条件恒不通过
    private static void HandlePredicate(EntitySelectorParser parser)
    {
        parser.ShouldInvertValue();
        IdentifierArgument.ReadIdentifier(parser.Reader);
        parser.AddPredicate(_ => false);
    }
}
