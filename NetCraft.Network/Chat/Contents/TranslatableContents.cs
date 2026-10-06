using System.Text.RegularExpressions;
using NetCraft.Codec;

namespace NetCraft.Network.Chat.Contents;

//Translatable contents, maps to vanilla net.minecraft.network.chat.contents.TranslatableContents
//A key + fallback + args triple forming a translation template, decomposed at runtime by a Language instance
public sealed class TranslatableContents : ComponentContents
{
    public string Key { get; }
    public string? Fallback { get; }
    public object[] Args { get; }

    //NO_ARGS empty argument array, maps to vanilla NO_ARGS
    public static readonly object[] NoArgs = Array.Empty<object>();

    //FORMAT_PATTERN placeholder pattern, maps to vanilla FORMAT_PATTERN
    //The two capture groups are the positional index and the format letter; when the positional index is omitted, arguments are taken in order of appearance
    private static readonly Regex FormatPattern =
        new(@"%(?:(\d+)\$)?([A-Za-z%]|$)", RegexOptions.Compiled);

    //The language instance the expansion is cached against, maps to vanilla decomposedWith; it only re-expands when the language changes
    private Language? _decomposedWith;
    private List<FormattedText> _decomposedParts = new();

    public TranslatableContents(string key, string? fallback, object[] args)
    {
        Key = key;
        Fallback = fallback;
        Args = args;
    }

    public MapCodec<ComponentContents> Codec() => throw new NotImplementedException();

    //Decompose splits the template into text segments for the current language, maps to vanilla decompose
    //When the template lacks a translation the key itself is used; a failed expansion outputs the segment as-is so no characters are lost
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

    //DecomposeTemplate replaces the %s placeholders segment by segment, maps to vanilla decomposeTemplate
    //A bare % or unsupported formats like %d in the template are treated as format errors, as in vanilla
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

    //GetArgument gets the argument at index, maps to vanilla getArgument
    //Component arguments keep their component identity so style and nested translations carry through; the rest are converted to strings
    private FormattedText GetArgument(int index)
    {
        if (index < 0 || index >= Args.Length) throw new FormatException();
        var arg = Args[index];
        if (arg is FormattedText text) return text;
        return FormattedText.Of(arg?.ToString() ?? "null");
    }

    //Visit unstyled consumer traverses the expanded text segments, maps to vanilla visit(ContentConsumer)
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

    //Visit styled consumer traverses the expanded text segments, maps to vanilla visit(StyledContentConsumer,Style)
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
