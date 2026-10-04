namespace NetCraft.Network.Chat;

//StringDecomposer 字符串拆解器对标原版 net.minecraft.util.StringDecomposer
//UTF-16 代理对解码 + § 颜色码解析遍历时回调 FormattedCharSink
//Iterate 纯解码不解析颜色码 IterateFormatted 含 § 颜色码解析 IterateBackwards 反向遍历
//孤立代理返回 ReplacementChar(U+FFFD) 对标原版 REPLACEMENT_CHAR
public static class StringDecomposer
{
    private const char ReplacementChar = '\uFFFD';

    //FeedChar 喂单个字符孤立代理返回 ReplacementChar 否则直接 codepoint
    //对标原版 feedChar 高低代理已在调用前单独处理此处兜底剩余代理
    private static bool FeedChar(Style style, FormattedCharSink output, int pos, char ch)
    {
        if (char.IsSurrogate(ch)) return output(pos, style, ReplacementChar);
        return output(pos, style, ch);
    }

    //Iterate 正向遍历 UTF-16 代理对解码对标原版 iterate
    //高代理后跟低代理合并为 codepoint 否则 ReplacementChar
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

    //IterateBackwards 反向遍历对标原版 iterateBackwards
    //从末尾向前低代理前跟高代理合并为 codepoint 否则 ReplacementChar
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

    //IterateFormatted 正向遍历含 § 颜色码解析对标原版 iterateFormatted
    //§=0xA7 后跟格式码 ChatFormatting.GetByCode 解析应用 ApplyLegacyFormat 到 style
    //RESET 重置为 resetStyle 其他格式累加到当前 style
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

    //FilterBrokenSurrogates 过滤损坏的代理对返回纯净字符串对标原版 filterBrokenSurrogates
    //孤立代理替换为 ReplacementChar 保留代理对
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
