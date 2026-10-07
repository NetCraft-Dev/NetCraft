using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Suggestion;
using NetCraft.Game.Server;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//BlockInWorld predicate evaluation context, maps to vanilla BlockInWorld
//The block entity is not on the level; the caller passes it in for the nbt predicate
public sealed record BlockInWorld(PersistentServerLevel Level, BlockPos Pos, BlockEntityManager? BlockEntities)
{
    public BlockState? State => Level.GetBlockState(Pos);
}

//BlockPredicateArgument block predicate argument, maps to vanilla net.minecraft.commands.arguments.blocks.BlockPredicateArgument
//Syntax: <block> | #<tag>, optionally followed by [property=value,...] and {block entity nbt}
//The property section only requires the listed properties to match and ignores the rest; the nbt section matches partially; given tags must be present in the actual tags
public sealed class BlockPredicateArgument : ArgumentType<Predicate<BlockInWorld>>
{
    public static readonly DynamicCommandExceptionType ErrorUnknownBlock =
        new(id => new LiteralMessage($"unknown block {id}"));

    public static readonly DynamicCommandExceptionType ErrorUnknownTag =
        new(id => new LiteralMessage($"unknown block tag {id}"));

    public static readonly SimpleCommandExceptionType ErrorInvalidState =
        new(new LiteralMessage("invalid block predicate syntax"));

    public static BlockPredicateArgument BlockPredicate() => new();

    public Predicate<BlockInWorld> Parse(StringReader reader)
    {
        var text = ReadToken(reader);

        //The predicate head ends at the first property or nbt section; the rest are parsed once each in order
        var headEnd = text.Length;
        var bracket = text.IndexOf('[');
        var brace = text.IndexOf('{');
        if (bracket >= 0 && bracket < headEnd) headEnd = bracket;
        if (brace >= 0 && brace < headEnd) headEnd = brace;
        var head = text[..headEnd];
        if (head.Length == 0) throw ErrorInvalidState.Create();

        var blockMatch = head[0] == '#' ? ParseTagMatch(head[1..]) : ParseBlockMatch(head);

        var cursor = headEnd;
        Func<BlockState, bool> propertyMatch = _ => true;
        if (cursor < text.Length && text[cursor] == '[')
        {
            var close = text.IndexOf(']', cursor);
            if (close < 0) throw ErrorInvalidState.Create();
            propertyMatch = ParsePropertyMatch(text[(cursor + 1)..close]);
            cursor = close + 1;
        }

        CompoundTag? nbtMatch = null;
        if (cursor < text.Length)
        {
            if (text[cursor] != '{' || !text.EndsWith('}')) throw ErrorInvalidState.Create();
            nbtMatch = TagParser<Tag>.ParseCompoundFully(text[cursor..]);
        }

        return world =>
        {
            var state = world.State;
            if (state is null) return false;
            if (!blockMatch(state.Value) || !propertyMatch(state.Value)) return false;
            if (nbtMatch is null) return true;
            var entity = world.BlockEntities?.Get(world.Pos);
            if (entity is null) return false;
            var tag = new CompoundTag();
            entity.SaveAdditional(tag);
            return MatchesTag(nbtMatch, tag);
        };
    }

    //ParseBlockMatch matches by block id; an unknown id errors out
    private static Func<BlockState, bool> ParseBlockMatch(string text)
    {
        var id = text.Contains(':') ? Identifier.TryParse(text) : Identifier.TryParse("minecraft:" + text);
        //BLOCK is a defaulted registry, so an unknown id resolves to air and must be caught with ContainsKey first
        if (id is null || !BuiltInRegistries.BLOCK.ContainsKey(id.Value))
            throw ErrorUnknownBlock.Create(text);
        var block = BuiltInRegistries.BLOCK.GetValue(id.Value)!;
        return state => ReferenceEquals(state.Owner, block);
    }

    //ParseTagMatch matches by block tag; an unregistered tag errors out
    private static Func<BlockState, bool> ParseTagMatch(string text)
    {
        var id = text.Contains(':') ? Identifier.TryParse(text) : Identifier.TryParse("minecraft:" + text);
        if (id is null) throw ErrorUnknownTag.Create(text);
        var key = TagKey<NetCraft.Registry.Block>.Create(Registries.BLOCK, id.Value);
        var set = BuiltInRegistries.BLOCK.Get(key);
        if (set is null) throw ErrorUnknownTag.Create(text);
        return state => set.IsBound && set.Any(holder => ReferenceEquals(holder.Value, state.Owner));
    }

    //ParsePropertyMatch matches properties one by one; entries with = require equal values, entries without only require the property to exist
    private static Func<BlockState, bool> ParsePropertyMatch(string text)
    {
        if (text.Length == 0) return _ => true;
        var conditions = new List<(string Name, string? Value)>();
        foreach (var pair in text.Split(','))
        {
            var separator = pair.IndexOf('=');
            if (separator < 0) conditions.Add((pair, null));
            else conditions.Add((pair[..separator], pair[(separator + 1)..]));
        }
        return state =>
        {
            foreach (var (name, value) in conditions)
            {
                var matched = false;
                foreach (var propertyValue in state.GetValues())
                {
                    if (propertyValue.Property.Name != name) continue;
                    matched = value is null || propertyValue.ValueName == value;
                    break;
                }
                if (!matched) return false;
            }
            return true;
        };
    }

    //MatchesTag partial match: every given entry must exist in the actual tag and be equal, maps to vanilla NbtPredicate
    //Lists require equal length and element-wise matching; other types use equality
    private static bool MatchesTag(Tag expected, Tag actual)
    {
        if (expected is CompoundTag expectedCompound)
        {
            if (actual is not CompoundTag actualCompound) return false;
            foreach (var key in expectedCompound.Keys)
            {
                if (!actualCompound.TryGetTag(key, out var actualValue)) return false;
                if (!MatchesTag(expectedCompound.Get<Tag>(key)!, actualValue)) return false;
            }
            return true;
        }
        if (expected is ListTag expectedList)
        {
            if (actual is not ListTag actualList || actualList.Count != expectedList.Count) return false;
            for (var i = 0; i < expectedList.Count; i++)
            {
                if (!MatchesTag(expectedList[i], actualList[i])) return false;
            }
            return true;
        }
        return expected.Equals(actual);
    }

    //ReadToken reads the whole predicate argument; quoted input goes through escape parsing, otherwise it reads to whitespace
    private static string ReadToken(StringReader reader)
    {
        if (reader.CanRead() && StringReader.IsQuotedStringStart(reader.Peek()))
            return reader.ReadQuotedString();
        var start = reader.Cursor;
        while (reader.CanRead() && !char.IsWhiteSpace(reader.Peek()))
            reader.Skip();
        return reader.String[start..reader.Cursor];
    }

    //GetBlockPredicate gets the parsed predicate
    public static Predicate<BlockInWorld> GetBlockPredicate(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<Predicate<BlockInWorld>>(name);

    //ListSuggestions suggests blocks or tags for the current cursor
    public Task<Suggestions> ListSuggestions<S>(CommandContext<S> context, SuggestionsBuilder builder)
    {
        var remaining = builder.Remaining;
        if (remaining.StartsWith('#'))
        {
            foreach (var set in BuiltInRegistries.BLOCK.GetTags())
            {
                var text = "#" + set.Key.Location.ToShortString();
                if (text.StartsWith(remaining, StringComparison.Ordinal))
                    builder.Add(text);
            }
        }
        else
        {
            foreach (var id in BuiltInRegistries.BLOCK.KeySet)
            {
                var text = id.ToShortString();
                if (text.StartsWith(remaining, StringComparison.Ordinal))
                    builder.Add(text);
            }
            if ("#".StartsWith(remaining, StringComparison.Ordinal)) builder.Add("#");
        }
        return builder.BuildFuture();
    }

    public IReadOnlyList<string> Examples => new[] { "stone", "minecraft:stone[axis=y]", "#minecraft:logs" };
}
