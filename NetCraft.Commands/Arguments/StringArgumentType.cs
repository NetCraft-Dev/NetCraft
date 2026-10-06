using System.Text;

using NetCraft.Commands.Context;

namespace NetCraft.Commands.Arguments;

//StringArgumentType string argument type, maps to vanilla com.mojang.brigadier.arguments.StringArgumentType
//Uses StringType to choose between three parsing modes with escape support
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

    //EscapeIfRequired calls Escape when the input contains characters not allowed unquoted
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

    //Escape wraps the input in double quotes, escaping backslashes and double quotes
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
