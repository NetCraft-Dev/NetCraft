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

    //GuardedPathCodec 限定在给定目录内的路径 codec 对应原版 ExtraCodecs.guardedPathCodec
    //只接受相对路径 且解析后仍落在这个目录里 越界一律拒绝
    public static Codec<string> GuardedPathCodec(string directory) => Codecs.String.ComapFlatMap(
        value => ValidateGuardedPath(directory, value),
        value => value);

    private static DataResult<string> ValidateGuardedPath(string directory, string path)
    {
        if (string.IsNullOrEmpty(path)) return DataResult<string>.Error(() => "Path must not be empty");
        if (Path.IsPathRooted(path)) return DataResult<string>.Error(() => $"Path must be relative: {path}");
        var basePath = Path.GetFullPath(directory);
        var fullPath = Path.GetFullPath(Path.Combine(basePath, path));
        var relative = Path.GetRelativePath(basePath, fullPath);
        if (relative.StartsWith("..") || Path.IsPathRooted(relative))
            return DataResult<string>.Error(() => $"Path escapes the guarded directory: {path}");
        return DataResult<string>.Success(path);
    }
}
