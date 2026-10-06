using NetCraft.Codec;

namespace NetCraft.Util;

//ExtraCodecs general supplementary codec, maps to vanilla net.minecraft.util.ExtraCodecs
//Only fills in the parts currently needed
public static class ExtraCodecs
{
    //IntRange integer codec limited to a closed interval, maps to vanilla ExtraCodecs.intRange
    public static Codec<int> IntRange(int min, int max) => Codecs.Int.ComapFlatMap(
        value => value >= min && value <= max
            ? DataResult<int>.Success(value)
            : DataResult<int>.Error(() => $"Value {value} outside of range [{min}; {max}]"),
        value => value);

    //GuardedPathCodec path codec restricted to a given directory, maps to vanilla ExtraCodecs.guardedPathCodec
    //Relative paths are normalized first: the first segment must not be . or .. nor empty, then it is attached under the directory to become absolute
    public static Codec<string> GuardedPathCodec(string directory) => Codecs.String.ComapFlatMap(
        value => ParseGuardedPath(directory, value),
        value => ToRelativePath(directory, value));

    private static DataResult<string> ParseGuardedPath(string directory, string text)
    {
        if (Path.IsPathRooted(text)) return DataResult<string>.Error(() => $"Illegal absolute path: {text}");
        var normalized = NormalizeRelative(text);
        if (normalized is null) return DataResult<string>.Error(() => $"Illegal path traversal: {text}");
        return DataResult<string>.Success(Path.Combine(Path.GetFullPath(directory), normalized));
    }

    private static string ToRelativePath(string directory, string value)
        => Path.GetRelativePath(Path.GetFullPath(directory), value).Replace('\\', '/');

    //NormalizeRelative normalizes per segment; empty paths and .. escaping the directory are both rejected
    private static string? NormalizeRelative(string text)
    {
        var segments = new List<string>();
        foreach (var raw in text.Replace('\\', '/').Split('/'))
        {
            if (raw.Length == 0 || raw == ".") continue;
            if (raw == "..")
            {
                if (segments.Count == 0) return null;
                segments.RemoveAt(segments.Count - 1);
                continue;
            }
            segments.Add(raw);
        }
        return segments.Count == 0 ? null : string.Join('/', segments);
    }
}
