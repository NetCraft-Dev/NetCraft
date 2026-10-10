using NetCraft.Codec;
using NetCraft.Util;

namespace NetCraft.Resources;

//PackFormat pack version descriptor, maps to vanilla net.minecraft.server.packs.metadata.pack.PackFormat
//26.2 replaced the single pack_format integer with a major/minor pair, where a minor of MaxMinor means any minor
public sealed record PackFormat(int Major, int Minor) : IComparable<PackFormat>
{
    //MaxMinor is the open upper bound of a minor range, rendered as "major.*"
    public const int MaxMinor = int.MaxValue;

    //Codec reads a version written either as an integer or as a two-element array, the shape supported_formats uses
    public static readonly Codec<PackFormat> Codec = new PackFormatCodec(0);

    public static PackFormat Of(int major, int minor) => new(major, minor);

    public static PackFormat Of(int major) => new(major, 0);

    public int CompareTo(PackFormat? other)
    {
        if (other is null) return 1;
        var majorDiff = Major.CompareTo(other.Major);
        return majorDiff != 0 ? majorDiff : Minor.CompareTo(other.Minor);
    }

    public override string ToString() => Minor == MaxMinor ? $"{Major}.*" : $"{Major}.{Minor}";
}

//PackFormatCodec reads a version from either a bare integer or a one/two-element array, mirroring the
//compactListCodec mapping vanilla PackFormat.fullCodec builds; the array form carries the minor explicitly
internal sealed class PackFormatCodec : ScalarCodec<PackFormat>
{
    private readonly int _defaultMinor;

    public PackFormatCodec(int defaultMinor) => _defaultMinor = defaultMinor;

    public override DataResult<PackFormat> Parse<U>(DynamicOps<U> ops, U input)
    {
        var direct = ops.GetNumberValue(input);
        if (direct.Result().IsPresent)
        {
            var major = (int)direct.GetOrThrow();
            return major < 0
                ? DataResult<PackFormat>.Error(() => "pack format must be non-negative")
                : DataResult<PackFormat>.Success(PackFormat.Of(major, _defaultMinor));
        }
        var stream = ops.GetStream(input);
        if (!stream.Result().IsPresent)
            return DataResult<PackFormat>.Error(() => "pack format must be an integer or an array of one or two integers");
        var parts = new List<int>();
        foreach (var element in stream.GetOrThrow())
        {
            var number = ops.GetNumberValue(element);
            if (!number.Result().IsPresent)
                return DataResult<PackFormat>.Error(() => "pack format elements must be integers");
            parts.Add((int)number.GetOrThrow());
        }
        return parts.Count switch
        {
            1 => parts[0] < 0
                ? DataResult<PackFormat>.Error(() => "pack format must be non-negative")
                : DataResult<PackFormat>.Success(PackFormat.Of(parts[0], _defaultMinor)),
            2 => DataResult<PackFormat>.Success(PackFormat.Of(parts[0], parts[1])),
            _ => DataResult<PackFormat>.Error(() => "pack format array must hold one or two integers")
        };
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, PackFormat value)
        => DataResult<U>.Success(value.Minor == _defaultMinor
            ? ops.CreateInt(value.Major)
            : ops.CreateList(new[] { ops.CreateInt(value.Major), ops.CreateInt(value.Minor) }));
}
