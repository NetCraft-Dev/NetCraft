namespace NetCraft.Network.Chat;

//StringDecomposer string decomposer, mirrors vanilla net.minecraft.util.StringDecomposer
//UTF-16 surrogate pair decoding + § color code parsing, invoking a FormattedCharSink during traversal
//Iterate does pure decoding without parsing color codes, IterateFormatted includes § color code parsing, IterateBackwards traverses backward
//A lone surrogate returns ReplacementChar(U+FFFD), mirrors vanilla REPLACEMENT_CHAR
public static class StringDecomposer
{
    private const char ReplacementChar = '\uFFFD';

    //FeedChar feeds a single character: a lone surrogate returns ReplacementChar, otherwise the codepoint directly
    //Mirrors vanilla feedChar; high and low surrogates are handled separately before the call, and this backstops any remaining surrogates
    private static bool FeedChar(Style style, FormattedCharSink output, int pos, char ch)
    {
        if (char.IsSurrogate(ch)) return output(pos, style, ReplacementChar);
        return output(pos, style, ch);
    }

    //Iterate forward traversal with UTF-16 surrogate pair decoding, mirrors vanilla iterate
    //A high surrogate followed by a low surrogate merges into a codepoint, otherwise ReplacementChar
    public static bool Iterate(string text, Style style, FormattedCharSink output)
    {
        int size = text.Length;
        int i = 0;
        while (i < size)
        {
            char ch = text[i];
            if (char.IsHighSurrogate(ch))
            {
                if (i + 1 >= size)
                {
                    if (!output(i, style, ReplacementChar)) return false;
                    return true;
                }
                char low = text[i + 1];
                if (char.IsLowSurrogate(low))
                {
                    if (!output(i, style, char.ConvertToUtf32(ch, low))) return false;
                    i++;
                }
                else if (!output(i, style, ReplacementChar)) return false;
            }
            else if (!FeedChar(style, output, i, ch)) return false;
            i++;
        }
        return true;
    }

    //IterateBackwards backward traversal, mirrors vanilla iterateBackwards
    //From the end backward, a low surrogate preceded by a high surrogate merges into a codepoint, otherwise ReplacementChar
    public static bool IterateBackwards(string text, Style style, FormattedCharSink output)
    {
        int size = text.Length;
        int i = size - 1;
        while (i >= 0)
        {
            char ch = text[i];
            if (char.IsLowSurrogate(ch))
            {
                if (i - 1 < 0)
                {
                    if (!output(0, style, ReplacementChar)) return false;
                    return true;
                }
                char high = text[i - 1];
                if (char.IsHighSurrogate(high))
                {
                    i--;
                    if (!output(i, style, char.ConvertToUtf32(high, ch))) return false;
                }
                else if (!output(i, style, ReplacementChar)) return false;
            }
            else if (!FeedChar(style, output, i, ch)) return false;
            i--;
        }
        return true;
    }

    //IterateFormatted forward traversal with § color code parsing, mirrors vanilla iterateFormatted
    //§=0xA7 followed by a format code; ChatFormatting.GetByCode parses it and ApplyLegacyFormat applies it to the style
    //RESET resets to resetStyle, while other formats accumulate onto the current style
    public static bool IterateFormatted(string text, Style style, FormattedCharSink output)
        => IterateFormatted(text, 0, style, output);

    public static bool IterateFormatted(string text, int offset, Style style, FormattedCharSink output)
        => IterateFormatted(text, offset, style, style, output);

    public static bool IterateFormatted(string text, int offset, Style currentStyle, Style resetStyle, FormattedCharSink output)
    {
        int size = text.Length;
        var style = currentStyle;
        int i = offset;
        while (i < size)
        {
            char ch = text[i];
            if (ch == '\u00A7')
            {
                if (i + 1 < size)
                {
                    char code = text[i + 1];
                    var formatting = ChatFormatting.GetByCode(code);
                    if (formatting != null)
                        style = formatting == ChatFormatting.Reset ? resetStyle : style.ApplyLegacyFormat(formatting);
                    i++;
                }
                else return true;
            }
            else if (char.IsHighSurrogate(ch))
            {
                if (i + 1 >= size)
                {
                    if (!output(i, style, ReplacementChar)) return false;
                    return true;
                }
                char low = text[i + 1];
                if (char.IsLowSurrogate(low))
                {
                    if (!output(i, style, char.ConvertToUtf32(ch, low))) return false;
                    i++;
                }
                else if (!output(i, style, ReplacementChar)) return false;
            }
            else if (!FeedChar(style, output, i, ch)) return false;
            i++;
        }
        return true;
    }

    //FilterBrokenSurrogates filters broken surrogate pairs and returns a clean string, mirrors vanilla filterBrokenSurrogates
    //Lone surrogates are replaced with ReplacementChar while surrogate pairs are kept
    public static string FilterBrokenSurrogates(string input)
    {
        var builder = new System.Text.StringBuilder();
        Iterate(input, Style.Empty, (position, style, codepoint) =>
        {
            builder.Append(char.ConvertFromUtf32(codepoint));
            return true;
        });
        return builder.ToString();
    }
}
