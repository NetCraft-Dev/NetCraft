using NetCraft.Codec;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//DimensionPadding dimension boundary padding, maps to vanilla net.minecraft.world.level.levelgen.structure.pools.DimensionPadding
//Number of blocks the structure bounding box must stay away from the dimension top/bottom; unequal top and bottom require the object form
public sealed record DimensionPadding(int Bottom, int Top)
{
    //Codec dimension padding codec; a bare int means both sides equal, the object form splits top and bottom, maps to vanilla CODEC
    public static readonly Codec<DimensionPadding> Codec = new DimensionPaddingCodec();

    //Zero no padding, maps to vanilla ZERO
    public static readonly DimensionPadding Zero = new(0);

    public DimensionPadding(int value)
        : this(value, value) { }

    //HasEqualTopAndBottom whether top and bottom padding are equal; encoding collapses to a bare int when equal
    public bool HasEqualTopAndBottom() => Top == Bottom;

    public override string ToString() => $"DimensionPadding[{Bottom},{Top}]";
}

//DimensionPaddingCodec dimension padding codec, either a bare int or a bottom/top object
internal sealed class DimensionPaddingCodec : ScalarCodec<DimensionPadding>
{
    public override DataResult<DimensionPadding> Parse<U>(DynamicOps<U> ops, U input)
    {
        var number = ops.GetNumberValue(input);
        if (number.Result().IsPresent)
        {
            var value = (int)number.GetOrThrow();
            return value < 0
                ? DataResult<DimensionPadding>.Error(() => $"dimension padding must be non-negative, got {value}")
                : DataResult<DimensionPadding>.Success(new DimensionPadding(value));
        }

        return ops.GetMap(input).FlatMap(map => ParsePadding(ops, map));
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, DimensionPadding value)
        => value.HasEqualTopAndBottom()
            ? DataResult<U>.Success(ops.CreateInt(value.Bottom))
            : BuildObject(ops, value);

    private static DataResult<DimensionPadding> ParsePadding<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var bottom = ReadPadding(ops, input, "bottom");
        if (!bottom.Result().IsPresent) return DataResult<DimensionPadding>.Error(() => "failed to parse dimension padding bottom");
        var top = ReadPadding(ops, input, "top");
        if (!top.Result().IsPresent) return DataResult<DimensionPadding>.Error(() => "failed to parse dimension padding top");
        return DataResult<DimensionPadding>.Success(new DimensionPadding(bottom.GetOrThrow(), top.GetOrThrow()));
    }

    //ReadPadding reads one side of the padding, missing field defaults to 0, maps to vanilla lenientOptionalFieldOf
    private static DataResult<int> ReadPadding<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var tag = input.Get(name);
        if (!tag.IsPresent) return DataResult<int>.Success(0);
        return ops.GetNumberValue(tag.Get()).FlatMap(raw =>
        {
            var value = (int)raw;
            return value < 0
                ? DataResult<int>.Error(() => $"dimension padding must be non-negative, got {value}")
                : DataResult<int>.Success(value);
        });
    }

    private static DataResult<U> BuildObject<U>(DynamicOps<U> ops, DimensionPadding value)
    {
        var builder = ops.MapBuilder();
        builder.Add("bottom", ops.CreateInt(value.Bottom));
        builder.Add("top", ops.CreateInt(value.Top));
        return builder.Build(ops.Empty());
    }
}
