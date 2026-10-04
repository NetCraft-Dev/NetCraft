using NetCraft.Codec;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//DimensionPadding 维度边界留白 对应原版 net.minecraft.world.level.levelgen.structure.pools.DimensionPadding
//结构包围盒不许贴到维度上下界的留白格数 上下不同就得写对象形态
public sealed record DimensionPadding(int Bottom, int Top)
{
    //Codec 维度留白编解码 裸整数两侧同值 对象形态分上下 对应原版 CODEC
    public static readonly Codec<DimensionPadding> Codec = new DimensionPaddingCodec();

    //Zero 不留白 对应原版 ZERO
    public static readonly DimensionPadding Zero = new(0);

    public DimensionPadding(int value)
        : this(value, value) { }

    //HasEqualTopAndBottom 上下留白是否相同 编码时相同就压成裸整数
    public bool HasEqualTopAndBottom() => Top == Bottom;

    public override string ToString() => $"DimensionPadding[{Bottom},{Top}]";
}

//DimensionPaddingCodec 维度留白编解码 裸整数或 bottom/top 对象二选一
internal sealed class DimensionPaddingCodec : ScalarCodec<DimensionPadding>
{
    public override DataResult<DimensionPadding> Parse<U>(DynamicOps<U> ops, U input)
    {
        var number = ops.GetNumberValue(input);
        if (number.Result().IsPresent)
        {
            var value = (int)number.GetOrThrow();
            return value < 0
                ? DataResult<DimensionPadding>.Error(() => $"维度留白必须非负 实际 {value}")
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
        if (!bottom.Result().IsPresent) return DataResult<DimensionPadding>.Error(() => "维度留白的 bottom 解析失败");
        var top = ReadPadding(ops, input, "top");
        if (!top.Result().IsPresent) return DataResult<DimensionPadding>.Error(() => "维度留白的 top 解析失败");
        return DataResult<DimensionPadding>.Success(new DimensionPadding(bottom.GetOrThrow(), top.GetOrThrow()));
    }

    //ReadPadding 读单侧留白 字段缺失按 0 对应原版 lenientOptionalFieldOf
    private static DataResult<int> ReadPadding<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var tag = input.Get(name);
        if (!tag.IsPresent) return DataResult<int>.Success(0);
        return ops.GetNumberValue(tag.Get()).FlatMap(raw =>
        {
            var value = (int)raw;
            return value < 0
                ? DataResult<int>.Error(() => $"维度留白必须非负 实际 {value}")
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
