using NetCraft.Commands;
using NetCraft.Commands.Exceptions;
using NetCraft.Game.World.Entity;
using NetCraft.Game.World.Level;
using NetCraft.Nbt;
using NetCraft.Registry;
using NetCraft.Util;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//EntitySelectorOptions selector option registry, maps to vanilla net.minecraft.commands.arguments.selector.options.EntitySelectorOptions
//17 [name=value] options; name/gamemode/type/team can be inverted, the rest parse per their own syntax
//team/tag/nbt/scores/advancements/predicate are downgraded to the player model: team has no team, tag no tags, nbt no serialized data, scores no scoreboard, advancements no advancements, predicate no loot predicate
public static class EntitySelectorOptions
{
    //Modifier option handler consumes the option value and rewrites the parser state
    public delegate void Modifier(EntitySelectorParser parser);

    //Option a single option, with a handler and an applicability predicate
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

    //Options the full option table, in the vanilla bootStrap registration order
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

    //Get fetches the option handler by name; unknown or unavailable for the current selector rolls back the cursor and throws
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

    //RollbackAndThrow rolls the cursor back to the option start then constructs the exception
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

    //HandleName player name filter, supporting quotes and inversion
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

    //HandleDistance distance range; negative values are invalid
    private static void HandleDistance(EntitySelectorParser parser)
    {
        var start = parser.Reader.Cursor;
        var value = MinMaxBounds.Doubles.FromReader(parser.Reader);
        if ((value.Min is not null && value.Min < 0.0) || (value.Max is not null && value.Max < 0.0))
            throw RollbackAndThrow(parser, start, ErrorRangeNegative);
        parser.SetDistance(value);
        parser.SetWorldLimited();
    }

    //HandleLevel experience level range; negative values are invalid, only players have levels
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

    //HandleLimit result limit is at least 1
    private static void HandleLimit(EntitySelectorParser parser)
    {
        var start = parser.Reader.Cursor;
        var count = parser.Reader.ReadInt();
        if (count < 1)
            throw RollbackAndThrow(parser, start, ErrorLimitTooSmall);
        parser.SetMaxResults(count);
        parser.LimitedOption.MarkParsed();
    }

    //HandleSort the four sort policies
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

    //HandleGamemode game type filter; inverted means not equal to that type
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

    //HandleTeam team filter; this project has no team system, so players always have an empty team name
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

    //HandleType entity type filter; a # prefix goes through type tags; player type taken positively excludes other entities
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
            //This project has no entity type tags, so players never match; inverted always passes
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

    //HandleTag entity tag filter; players always have no tags; an empty value means those without tags
    private static void HandleTag(EntitySelectorParser parser)
    {
        var inverted = parser.ShouldInvertValue();
        var tag = parser.Reader.ReadUnquotedString();
        parser.AddPredicate(_ => (tag.Length == 0) != inverted);
    }

    //HandleNbt NBT filter; SNBT syntax parsed like vanilla; players have no serialized NBT and never match; inverted always passes
    private static void HandleNbt(EntitySelectorParser parser)
    {
        var inverted = parser.ShouldInvertValue();
        //The SNBT parser uses a separate reader and syncs the cursor after parsing
        var nbtReader = new CommandStringReader(parser.Reader.String) { Cursor = parser.Reader.Cursor };
        TagParser<object>.ParseCompoundAsArgument(nbtReader);
        parser.Reader.SetCursor(nbtReader.Cursor);
        parser.AddPredicate(_ => inverted);
    }

    //HandleScores scoreboard filter {objective=range,...} parsed like vanilla; this project has no scoreboard condition and always fails
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

    //HandleAdvancements advancement filter {advancement=bool or {criterion=bool},...} parsed like vanilla; no advancements here and always fails
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

    //HandlePredicate loot predicate filter; no predicate library here and always fails
    private static void HandlePredicate(EntitySelectorParser parser)
    {
        parser.ShouldInvertValue();
        IdentifierArgument.ReadIdentifier(parser.Reader);
        parser.AddPredicate(_ => false);
    }
}
