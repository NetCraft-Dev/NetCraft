namespace NetCraft.Commands.Arguments;

//StringType string argument type enum, maps to vanilla com.mojang.brigadier.arguments.StringArgumentType.StringType
//A C# enum cannot hold a String[] field, so this becomes a sealed class with three static instances carrying the Examples list
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
