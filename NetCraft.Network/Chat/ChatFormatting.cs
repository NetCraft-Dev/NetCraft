namespace NetCraft.Network.Chat;

using System.Text.RegularExpressions;

//Chat formatting codes, maps to vanilla net.minecraft.ChatFormatting
//The vanilla enum of § + character formatting codes, used for legacy text styling
//Java enums with fields are unsupported in C#, so it becomes a sealed class with static readonly instances
public sealed class ChatFormatting
{
    public static readonly ChatFormatting Black = new('0', "black");
    public static readonly ChatFormatting DarkBlue = new('1', "dark_blue");
    public static readonly ChatFormatting DarkGreen = new('2', "dark_green");
    public static readonly ChatFormatting DarkAqua = new('3', "dark_aqua");
    public static readonly ChatFormatting DarkRed = new('4', "dark_red");
    public static readonly ChatFormatting DarkPurple = new('5', "dark_purple");
    public static readonly ChatFormatting Gold = new('6', "gold");
    public static readonly ChatFormatting Gray = new('7', "gray");
    public static readonly ChatFormatting DarkGray = new('8', "dark_gray");
    public static readonly ChatFormatting Blue = new('9', "blue");
    public static readonly ChatFormatting Green = new('a', "green");
    public static readonly ChatFormatting Aqua = new('b', "aqua");
    public static readonly ChatFormatting Red = new('c', "red");
    public static readonly ChatFormatting LightPurple = new('d', "light_purple");
    public static readonly ChatFormatting Yellow = new('e', "yellow");
    public static readonly ChatFormatting White = new('f', "white");
    public static readonly ChatFormatting Obfuscated = new('k', "obfuscated");
    public static readonly ChatFormatting Bold = new('l', "bold");
    public static readonly ChatFormatting Strikethrough = new('m', "strikethrough");
    public static readonly ChatFormatting Underline = new('n', "underline");
    public static readonly ChatFormatting Italic = new('o', "italic");
    public static readonly ChatFormatting Reset = new('r', "reset");

    public const char PREFIX_CODE = '\u00a7';

    private static readonly Regex StripFormattingPattern = new("(?i)\u00a7[0-9A-FK-OR]", RegexOptions.Compiled);

    private static readonly ChatFormatting[] AllValues =
    {
        Black, DarkBlue, DarkGreen, DarkAqua, DarkRed, DarkPurple, Gold, Gray,
        DarkGray, Blue, Green, Aqua, Red, LightPurple, Yellow, White,
        Obfuscated, Bold, Strikethrough, Underline, Italic, Reset,
    };

    private readonly char _code;
    private readonly string _name;

    private ChatFormatting(char code, string name)
    {
        _code = code;
        _name = name;
        ToStringValue = $"{PREFIX_CODE}{code}";
    }

    public char Code => _code;
    public string Name => _name;
    private string ToStringValue { get; }

    //All formatting code values, maps to vanilla values()
    public static IReadOnlyList<ChatFormatting> Values() => AllValues;

    public override string ToString() => ToStringValue;

    //Removes all formatting codes from a string, maps to vanilla stripFormatting
    public static string? StripFormatting(string? input)
    {
        if (input is null) return null;
        return StripFormattingPattern.Replace(input, string.Empty);
    }

    //Looks up by code character, maps to vanilla getByCode
    public static ChatFormatting? GetByCode(char code)
    {
        var sanitized = char.ToLowerInvariant(code);
        foreach (var format in AllValues)
        {
            if (format._code == sanitized) return format;
        }
        return null;
    }

    //Looks up by name, maps to vanilla getByName
    public static ChatFormatting? GetByName(string name)
    {
        foreach (var format in AllValues)
        {
            if (format._name == name) return format;
        }
        return null;
    }

    //switch is unsuitable for formatting code lookup because C# cannot range-match sealed instances in a pattern
    //Instead GetByCode returns null to achieve equivalent semantics
}
