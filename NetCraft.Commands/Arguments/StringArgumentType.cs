using System.Text;

using NetCraft.Commands.Context;

namespace NetCraft.Commands.Arguments;

//StringArgumentType 字符串参数类型对应原版com.mojang.brigadier.arguments.StringArgumentType
//按StringType分三种解析模式并支持转义
public sealed class StringArgumentType : ArgumentType<string>
{
    private readonly StringType _type;

    private StringArgumentType(StringType type)
    {
        _type = type;
    }

    public static StringArgumentType Word() => new(StringType.SingleWord);

    public static StringArgumentType String() => new(StringType.QuotablePhrase);

    public static StringArgumentType GreedyString() => new(StringType.GreedyPhrase);

    public static string GetString<S>(CommandContext<S> context, string name)
    {
        return context.GetArgument<string>(name);
    }

    public StringType Type => _type;

    public string Parse(StringReader reader)
    {
        if (ReferenceEquals(_type, StringType.GreedyPhrase))
        {
            var text = reader.Remaining;
            reader.SetCursor(reader.TotalLength);
            return text;
        }
        if (ReferenceEquals(_type, StringType.SingleWord))
        {
            return reader.ReadUnquotedString();
        }
        return reader.ReadString();
    }

    public IReadOnlyList<string> Examples => _type.Examples;

    public override string ToString() => "string()";

    //EscapeIfRequired 含非无引号允许字符则调用Escape转义
    public static string EscapeIfRequired(string input)
    {
        foreach (var c in input)
        {
            if (!StringReader.IsAllowedInUnquotedString(c))
            {
                return Escape(input);
            }
        }
        return input;
    }

    //Escape 用双引号包裹并转义反斜杠与双引号
    private static string Escape(string input)
    {
        var result = new StringBuilder("\"");
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (c == '\\' || c == '"')
            {
                result.Append('\\');
            }
            result.Append(c);
        }
        result.Append('"');
        return result.ToString();
    }
}
