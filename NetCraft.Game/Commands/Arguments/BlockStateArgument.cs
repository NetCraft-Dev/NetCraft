using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Exceptions;
using NetCraft.Nbt;
using NetCraft.Registry;
using NetCraft.Registry.State;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//BlockInput block argument parse result, maps to vanilla BlockInput
//Nbt is the optional block entity data, written after the block state in the syntax
public sealed record BlockInput(BlockState State, CompoundTag? Nbt);

//BlockStateArgument block state argument, maps to vanilla net.minecraft.commands.arguments.blocks.BlockStateArgument
//Syntax: namespace:path[property=value,...]{block entity nbt}; both properties and nbt are optional
//When the nbt contains spaces the whole segment is wrapped in double quotes and parsed inside by escape rules
public sealed class BlockStateArgument : ArgumentType<BlockInput>
{
    public static readonly DynamicCommandExceptionType ErrorUnknownBlock =
        new(id => new LiteralMessage($"unknown block {id}"));

    public static readonly SimpleCommandExceptionType ErrorInvalidState =
        new(new LiteralMessage("invalid block state syntax"));

    public static readonly SimpleCommandExceptionType ErrorUnknownProperty =
        new(new LiteralMessage("block does not have that property"));

    public static readonly SimpleCommandExceptionType ErrorInvalidPropertyValue =
        new(new LiteralMessage("invalid property value"));

    public static BlockStateArgument Block() => new();

    public BlockInput Parse(StringReader reader)
    {
        var text = ReadToken(reader);

        //The state part ends at the first property or nbt section; the rest are parsed once each in order
        var stateEnd = text.Length;
        var bracket = text.IndexOf('[');
        var brace = text.IndexOf('{');
        if (bracket >= 0 && bracket < stateEnd) stateEnd = bracket;
        if (brace >= 0 && brace < stateEnd) stateEnd = brace;

        var stateText = text[..stateEnd];
        var identifier = stateText.Contains(':')
            ? Identifier.TryParse(stateText)
            : Identifier.TryParse("minecraft:" + stateText);
        //BLOCK is a defaulted registry, so an unknown id resolves to air and must be caught with ContainsKey first
        if (identifier is null || !BuiltInRegistries.BLOCK.ContainsKey(identifier.Value))
            throw ErrorUnknownBlock.Create(stateText);
        var state = BuiltInRegistries.BLOCK.GetValue(identifier.Value)!.DefaultBlockState;
        var cursor = stateEnd;
        if (cursor < text.Length && text[cursor] == '[')
        {
            var close = text.IndexOf(']', cursor);
            if (close < 0) throw ErrorInvalidState.Create();
            state = ApplyProperties(state, text[(cursor + 1)..close]);
            cursor = close + 1;
        }

        CompoundTag? nbt = null;
        if (cursor < text.Length)
        {
            if (text[cursor] != '{' || !text.EndsWith('}')) throw ErrorInvalidState.Create();
            nbt = TagParser<Tag>.ParseCompoundFully(text[cursor..]);
        }
        return new BlockInput(state, nbt);
    }

    //ApplyProperties matches property names one by one and reads values; an unknown name or value is a syntax error
    private static BlockState ApplyProperties(BlockState state, string text)
    {
        if (text.Length == 0) return state;
        foreach (var pair in text.Split(','))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0) throw ErrorInvalidState.Create();
            var name = pair[..separator];
            var value = pair[(separator + 1)..];

            PropertyBase? property = null;
            foreach (var candidate in state.GetProperties())
            {
                if (candidate.Name != name) continue;
                property = candidate;
                break;
            }
            if (property is null) throw ErrorUnknownProperty.Create();
            var parsed = property.GetValueForName(value) ?? throw ErrorInvalidPropertyValue.Create();
            state = state.SetValue(property, parsed);
        }
        return state;
    }

    //ReadToken reads the whole block argument; quoted input goes through escape parsing, otherwise it reads to whitespace
    private static string ReadToken(StringReader reader)
    {
        if (reader.CanRead() && StringReader.IsQuotedStringStart(reader.Peek()))
            return reader.ReadQuotedString();
        var start = reader.Cursor;
        while (reader.CanRead() && !char.IsWhiteSpace(reader.Peek()))
            reader.Skip();
        return reader.String[start..reader.Cursor];
    }

    public IReadOnlyList<string> Examples => new[] { "minecraft:stone", "grass_block[snowy=true]", "chest{}" };
}
