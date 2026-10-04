using NetCraft.Codec;

namespace NetCraft.Util;

//ExtraCodecs 通用的补充 codec 对应原版 net.minecraft.util.ExtraCodecs
//只补当前用得到的部分
public static class ExtraCodecs
{
    //IntRange 限定闭区间的整数 codec 对应原版 ExtraCodecs.intRange
    public static Codec<int> IntRange(int min, int max) => Codecs.Int.ComapFlatMap(
        value => value >= min && value <= max
            ? DataResult<int>.Success(value)
            : DataResult<int>.Error(() => $"Value {value} outside of range [{min}; {max}]"),
        value => value);

    //GuardedPathCodec 限定在给定目录下的路径 codec 对应原版 ExtraCodecs.guardedPathCodec
    //相对路径先归一 首段不许是 . 或 .. 也不许为空 再挂到目录下成为绝对路径
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

    //NormalizeRelative 按段归一 空路径与跳出目录的 .. 都判非法
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
