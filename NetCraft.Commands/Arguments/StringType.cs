namespace NetCraft.Commands.Arguments;

//StringType 字符串参数类型枚举对应原版com.mojang.brigadier.arguments.StringArgumentType.StringType
//C# enum不能持String[]字段故改sealed class三静态实例持Examples列表
public sealed class StringType
{
    public static readonly StringType SingleWord = new(new[] { "word", "words_with_underscores" });
    public static readonly StringType QuotablePhrase = new(new[] { "\"quoted phrase\"", "word", "\"\"" });
    public static readonly StringType GreedyPhrase = new(new[] { "word", "words with spaces", "\"and symbols\"" });

    public IReadOnlyList<string> Examples { get; }

    private StringType(string[] examples)
    {
        Examples = examples;
    }
}
