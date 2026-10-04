using NetCraft.Codec;
using NetCraft.Util;
using NetCraft.Util.Parsing.Packrat.Commands;

namespace NetCraft.Nbt;

//SNBT解析器入口对应原版net.minecraft.nbt.TagParser
//包装Grammar提供parseFully/parseAsArgument方法
//FLATTENED_CODEC从字符串解析CompoundTag LENIENT_CODEC兼容CompoundTag.CODEC
public sealed class TagParser<T>
{
    public const char ElementSeparator = ',';
    public const char NameValueSeparator = ':';

    //NbtOps下的解析器实例共享给静态方法
    private static readonly TagParser<Tag> NbtOpsParser = Create(NbtOps.Instance);

    public static readonly SimpleCommandExceptionType ErrorTrailingData =
        new("Trailing data found");

    public static readonly SimpleCommandExceptionType ErrorExpectedCompound =
        new("Expected compound tag");

    //FLATTENED_CODEC从字符串解析成CompoundTag失败抛异常失败时反向toString
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

    //LENIENT_CODEC先尝试FLATTENED_CODEC失败再尝试CompoundTag.CODEC
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

    //create工厂用SnbtGrammar.CreateParser构造grammar
    //U是方法泛型参数独立于类泛型T对应原版static <T> create方法
    public static TagParser<U> Create<U>(DynamicOps<U> ops)
        => new(ops, SnbtGrammar.CreateParser(ops));

    //castToCompoundOrThrow非CompoundTag抛ErrorExpectedCompound
    private static CompoundTag CastToCompoundOrThrow(CommandStringReader reader, Tag result)
    {
        if (result is CompoundTag compoundTag) return compoundTag;
        throw ErrorExpectedCompound.CreateWithContext(reader);
    }

    //parseCompoundFully解析完整字符串强制返回CompoundTag
    public static CompoundTag ParseCompoundFully(string input)
    {
        var reader = new CommandStringReader(input);
        return CastToCompoundOrThrow(reader, NbtOpsParser.ParseFully(reader));
    }

    //parseFully解析完整字符串尾部有剩余字符报ErrorTrailingData
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

    //parseAsArgument只解析不要求尾部为空
    public T ParseAsArgument(CommandStringReader reader)
        => _grammar.ParseForCommands(reader);

    //parseCompoundAsArgument用NbtOpsParser解析后强制返回CompoundTag
    public static CompoundTag ParseCompoundAsArgument(CommandStringReader reader)
        => CastToCompoundOrThrow(reader, NbtOpsParser.ParseAsArgument(reader));

    //ParseTagAsArgument 静态入口解析任意标签不要求尾部为空 对应原版 TagParser.parseTag
    //名字带 AsArgument 区分实例版 parseAsArgument 静态实例方法不能同名
    public static Tag ParseTagAsArgument(CommandStringReader reader)
        => NbtOpsParser.ParseAsArgument(reader);
}
