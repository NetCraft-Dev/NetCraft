using NetCraft.Codec;

namespace NetCraft.Game.World.Level.LevelGen;

//VerticalAnchor 垂直锚点对应原版 net.minecraft.world.level.levelgen.VerticalAnchor
//JSON 三选一 absolute/above_bottom/below_top 供表面规则把锚点解算成绝对 y
public sealed class VerticalAnchor
{
    //AnchorKind 锚点三种形式对应原版 Absolute/AboveBottom/BelowTop
    public enum AnchorKind
    {
        Absolute,
        AboveBottom,
        BelowTop
    }

    public AnchorKind Kind { get; }

    //Value absolute 时是绝对 y 其余是相对底部/顶部的偏移
    public int Value { get; }

    private VerticalAnchor(AnchorKind kind, int value)
    {
        Kind = kind;
        Value = value;
    }

    public static VerticalAnchor Absolute(int y) => new(AnchorKind.Absolute, y);

    public static VerticalAnchor AboveBottom(int offset) => new(AnchorKind.AboveBottom, offset);

    public static VerticalAnchor BelowTop(int offset) => new(AnchorKind.BelowTop, offset);

    //ResolveY 把锚点解算成绝对 y 对应原版 resolveY
    public int ResolveY(int minY, int height) => Kind switch
    {
        AnchorKind.Absolute => Value,
        AnchorKind.AboveBottom => minY + Value,
        _ => minY + height - 1 - Value
    };

    //ResolveY 用世界生成上下文解算对应原版 resolveY(WorldGenerationContext)
    public int ResolveY(WorldGenerationContext context) => ResolveY(context.GetMinGenY(), context.GetGenDepth());

    //Codec 三选一单键对象编解码对应原版 VerticalAnchor.CODEC
    public static readonly Codec<VerticalAnchor> Codec = new VerticalAnchorCodec();

    public override string ToString() => Kind switch
    {
        AnchorKind.Absolute => $"{Value} absolute",
        AnchorKind.AboveBottom => $"{Value} above bottom",
        _ => $"{Value} below top"
    };
}

//VerticalAnchorCodec 单键对象编解码
//三选一按原版 xor codec 顺序 absolute/above_bottom/below_top 尝试
internal sealed class VerticalAnchorCodec : ScalarCodec<VerticalAnchor>
{
    public override DataResult<VerticalAnchor> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodeAnchor(ops, map));

    private static DataResult<VerticalAnchor> DecodeAnchor<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var absolute = ReadValue(ops, input, "absolute");
        if (absolute is not null) return DataResult<VerticalAnchor>.Success(VerticalAnchor.Absolute(absolute.Value));
        var aboveBottom = ReadValue(ops, input, "above_bottom");
        if (aboveBottom is not null) return DataResult<VerticalAnchor>.Success(VerticalAnchor.AboveBottom(aboveBottom.Value));
        var belowTop = ReadValue(ops, input, "below_top");
        if (belowTop is not null) return DataResult<VerticalAnchor>.Success(VerticalAnchor.BelowTop(belowTop.Value));
        return DataResult<VerticalAnchor>.Error(() => "VerticalAnchor requires one of absolute/above_bottom/below_top");
    }

    //ReadValue 读单键整数值 字段缺失返回 null 非数字返回 null 由调用方统一报错
    private static int? ReadValue<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var tag = input.Get(name);
        if (!tag.IsPresent) return null;
        var value = ops.GetNumberValue(tag.Get());
        if (!value.Result().IsPresent) return null;
        return (int)value.GetOrThrow();
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, VerticalAnchor value)
    {
        var name = value.Kind switch
        {
            VerticalAnchor.AnchorKind.Absolute => "absolute",
            VerticalAnchor.AnchorKind.AboveBottom => "above_bottom",
            _ => "below_top"
        };
        var builder = ops.MapBuilder();
        builder.Add(name, ops.CreateInt(value.Value));
        return builder.Build(ops.Empty());
    }
}
