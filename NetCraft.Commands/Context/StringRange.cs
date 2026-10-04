using NetCraft.Commands;

namespace NetCraft.Commands.Context;

//StringRange 字符串范围对应原版com.mojang.brigadier.context.StringRange
//标记解析片段在原输入中的起止位置供ParsedArgument与Suggestion使用
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
