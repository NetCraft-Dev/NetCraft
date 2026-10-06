using NetCraft.Commands;

namespace NetCraft.Commands.Context;

//StringRange maps to vanilla com.mojang.brigadier.context.StringRange
//Marks the start and end of a parsed fragment in the original input, used by ParsedArgument and Suggestion
public sealed record StringRange(int Start, int End)
{
    public static StringRange At(int pos) => new(pos, pos);

    public static StringRange Between(int start, int end) => new(start, end);

    public static StringRange Encompassing(StringRange a, StringRange b)
        => new(Math.Min(a.Start, b.Start), Math.Max(a.End, b.End));

    public string Get(IImmutableStringReader reader) => reader.String[Start..End];

    public string Get(string @string) => @string[Start..End];

    public bool IsEmpty() => Start == End;

    public int Length => End - Start;
}
