using NetCraft.Codec;
using NetCraft.Util;
using NetCraft.Util.Parsing.Packrat.Commands;

namespace NetCraft.Nbt;

//SNBT parser entry point, mirroring vanilla net.minecraft.nbt.TagParser
//Wraps a Grammar and exposes parseFully/parseAsArgument
//FLATTENED_CODEC parses a CompoundTag from a string, LENIENT_CODEC is compatible with CompoundTag.CODEC
public sealed class TagParser<T>
{
    public const char ElementSeparator = ',';
    public const char NameValueSeparator = ':';

    //Parser instance under NbtOps, shared with the static methods
    private static readonly TagParser<Tag> NbtOpsParser = Create(NbtOps.Instance);

    public static readonly SimpleCommandExceptionType ErrorTrailingData =
        new("Trailing data found");

    public static readonly SimpleCommandExceptionType ErrorExpectedCompound =
        new("Expected compound tag");

    //FLATTENED_CODEC parses a CompoundTag from a string and throws on failure, rendering the input back with toString for the message
    public static readonly Codec<CompoundTag> FlattenedCodec = Codecs.String.ComapFlatMap(
        s =>
        {
            try
            {
                var result = NbtOpsParser.ParseFully(s);
                if (result is CompoundTag compoundTag)
                    return DataResult<CompoundTag>.Success(compoundTag);
                return DataResult<CompoundTag>.Error(() => "Expected compound tag, got " + result);
            }
            catch (CommandSyntaxException e)
            {
                return DataResult<CompoundTag>.Error(() => e.Message);
            }
        },
        v => v.ToString());

    //LENIENT_CODEC tries FLATTENED_CODEC first and falls back to CompoundTag.CODEC
    public static readonly Codec<CompoundTag> LenientCodec =
        Codecs.WithAlternative(FlattenedCodec, CompoundTag.Codec);

    private readonly DynamicOps<T> _ops;
    private readonly Grammar<T> _grammar;

    private TagParser(DynamicOps<T> ops, Grammar<T> grammar)
    {
        _ops = ops;
        _grammar = grammar;
    }

    public DynamicOps<T> Ops => _ops;

    //The create factory builds the grammar with SnbtGrammar.CreateParser
    //U is a method type parameter independent of the class parameter T, mirroring the vanilla static <T> create method
    public static TagParser<U> Create<U>(DynamicOps<U> ops)
        => new(ops, SnbtGrammar.CreateParser(ops));

    //castToCompoundOrThrow throws ErrorExpectedCompound for a non-CompoundTag
    private static CompoundTag CastToCompoundOrThrow(CommandStringReader reader, Tag result)
    {
        if (result is CompoundTag compoundTag) return compoundTag;
        throw ErrorExpectedCompound.CreateWithContext(reader);
    }

    //parseCompoundFully parses the whole string and forces a CompoundTag result
    public static CompoundTag ParseCompoundFully(string input)
    {
        var reader = new CommandStringReader(input);
        return CastToCompoundOrThrow(reader, NbtOpsParser.ParseFully(reader));
    }

    //parseFully parses the whole string and reports ErrorTrailingData when characters are left over
    public T ParseFully(string input)
        => ParseFully(new CommandStringReader(input));

    public T ParseFully(CommandStringReader reader)
    {
        var result = _grammar.ParseForCommands(reader);
        reader.SkipWhitespace();
        if (reader.CanRead())
            throw ErrorTrailingData.CreateWithContext(reader);
        return result;
    }

    //parseAsArgument parses without requiring the input to be fully consumed
    public T ParseAsArgument(CommandStringReader reader)
        => _grammar.ParseForCommands(reader);

    //parseCompoundAsArgument parses with NbtOpsParser and forces a CompoundTag result
    public static CompoundTag ParseCompoundAsArgument(CommandStringReader reader)
        => CastToCompoundOrThrow(reader, NbtOpsParser.ParseAsArgument(reader));

    //ParseTagAsArgument is the static entry point for parsing any tag without requiring full consumption. Mirrors vanilla TagParser.parseTag
    //The AsArgument suffix distinguishes it from the instance parseAsArgument, since the static and instance methods cannot share a name
    public static Tag ParseTagAsArgument(CommandStringReader reader)
        => NbtOpsParser.ParseAsArgument(reader);
}
