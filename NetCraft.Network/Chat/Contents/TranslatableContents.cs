using System.Text.RegularExpressions;
using NetCraft.Codec;

namespace NetCraft.Network.Chat.Contents;

//翻译内容对应原版net.minecraft.network.chat.contents.TranslatableContents
//key + fallback + args 三元组翻译模板运行时按 Language 实例 decompose
public sealed class TranslatableContents : ComponentContents
{
    public string Key { get; }
    public string? Fallback { get; }
    public object[] Args { get; }

    //NO_ARGS 空参数数组对应原版 NO_ARGS
    public static readonly object[] NoArgs = Array.Empty<object>();

    //FORMAT_PATTERN 占位符模式对应原版 FORMAT_PATTERN
    //两段分组依次是位置序号与格式字母 位置序号缺省时按出现顺序取参数
    private static readonly Regex FormatPattern =
        new(@"%(?:(\d+)\$)?([A-Za-z%]|$)", RegexOptions.Compiled);

    //展开缓存的语言实例对应原版 decomposedWith 换语言才重新展开
    private Language? _decomposedWith;
    private List<FormattedText> _decomposedParts = new();

    public TranslatableContents(string key, string? fallback, object[] args)
    {
        Key = key;
        Fallback = fallback;
        Args = args;
    }

    public MapCodec<ComponentContents> Codec() => throw new NotImplementedException();

    //Decompose 按当前语言把模板拆成文本段 对应原版 decompose
    //模板缺翻译时取 key 本身 展开失败整段原样输出不至于丢字
    private void Decompose()
    {
        var current = Language.Instance;
        if (ReferenceEquals(current, _decomposedWith)) return;
        _decomposedWith = current;
        var format = Fallback is not null
            ? current.GetOrDefault(Key, Fallback)
            : current.GetOrDefault(Key);
        try
        {
            _decomposedParts = DecomposeTemplate(format);
        }
        catch (FormatException)
        {
            _decomposedParts = new List<FormattedText> { FormattedText.Of(format) };
        }
    }

    //DecomposeTemplate 逐段替换 %s 占位 对应原版 decomposeTemplate
    //模板里出现裸 % 或 %d 这类不支持的格式按原版判为格式错误
    private List<FormattedText> DecomposeTemplate(string template)
    {
        var parts = new List<FormattedText>();
        var replacementIndex = 0;
        var current = 0;
        foreach (Match match in FormatPattern.Matches(template))
        {
            var start = match.Index;
            var end = start + match.Length;
            if (start > current)
            {
                var prefix = template[current..start];
                if (prefix.Contains('%')) throw new FormatException();
                parts.Add(FormattedText.Of(prefix));
            }
            var formatType = match.Groups[2].Value;
            if (formatType == "%" && match.Value == "%%")
            {
                parts.Add(FormattedText.Of("%"));
            }
            else if (formatType == "s")
            {
                var index = match.Groups[1].Success
                    ? int.Parse(match.Groups[1].Value) - 1
                    : replacementIndex++;
                parts.Add(GetArgument(index));
            }
            else
            {
                throw new FormatException();
            }
            current = end;
        }
        if (current < template.Length)
        {
            var tail = template[current..];
            if (tail.Contains('%')) throw new FormatException();
            parts.Add(FormattedText.Of(tail));
        }
        return parts;
    }

    //GetArgument 取第 index 个参数 对应原版 getArgument
    //组件参数保持组件身份让样式与嵌套翻译跟着走 其余转字符串
    private FormattedText GetArgument(int index)
    {
        if (index < 0 || index >= Args.Length) throw new FormatException();
        var arg = Args[index];
        if (arg is FormattedText text) return text;
        return FormattedText.Of(arg?.ToString() ?? "null");
    }

    //Visit 无样式消费者遍历展开后的文本段 对应原版 visit(ContentConsumer)
    public Optional<T> Visit<T>(FormattedText.ContentConsumer<T> output)
    {
        Decompose();
        foreach (var part in _decomposedParts)
        {
            var result = part.Visit(output);
            if (result.IsPresent) return result;
        }
        return Optional<T>.Empty();
    }

    //Visit 带样式消费者遍历展开后的文本段 对应原版 visit(StyledContentConsumer,Style)
    public Optional<T> Visit<T>(FormattedText.StyledContentConsumer<T> output, Style currentStyle)
    {
        Decompose();
        foreach (var part in _decomposedParts)
        {
            var result = part.Visit(output, currentStyle);
            if (result.IsPresent) return result;
        }
        return Optional<T>.Empty();
    }

    public override bool Equals(object? obj)
    {
        if (this == obj) return true;
        if (obj is not TranslatableContents that) return false;
        return Key == that.Key && Fallback == that.Fallback && Args.SequenceEqual(that.Args);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Key);
        hash.Add(Fallback);
        foreach (var arg in Args) hash.Add(arg);
        return hash.ToHashCode();
    }

    public override string ToString()
        => $"translation{{key='{Key}'{(Fallback is not null ? $", fallback='{Fallback}'" : "")}, args={string.Join(",", Args)}}}";
}
