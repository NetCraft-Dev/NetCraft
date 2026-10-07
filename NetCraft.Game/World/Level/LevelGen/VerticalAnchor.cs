using NetCraft.Codec;

namespace NetCraft.Game.World.Level.LevelGen;

//VerticalAnchor vertical anchor, maps to vanilla net.minecraft.world.level.levelgen.VerticalAnchor
//JSON picks one of absolute/above_bottom/below_top; surface rules use it to resolve the anchor into an absolute y
public sealed class VerticalAnchor
{
    //AnchorKind the three anchor forms, maps to vanilla Absolute/AboveBottom/BelowTop
    public enum AnchorKind
    {
        Absolute,
        AboveBottom,
        BelowTop
    }

    public AnchorKind Kind { get; }

    //Value is the absolute y when absolute, otherwise the offset from the bottom/top
    public int Value { get; }

    private VerticalAnchor(AnchorKind kind, int value)
    {
        Kind = kind;
        Value = value;
    }

    public static VerticalAnchor Absolute(int y) => new(AnchorKind.Absolute, y);

    public static VerticalAnchor AboveBottom(int offset) => new(AnchorKind.AboveBottom, offset);

    public static VerticalAnchor BelowTop(int offset) => new(AnchorKind.BelowTop, offset);

    //ResolveY resolves the anchor into an absolute y, maps to vanilla resolveY
    public int ResolveY(int minY, int height) => Kind switch
    {
        AnchorKind.Absolute => Value,
        AnchorKind.AboveBottom => minY + Value,
        _ => minY + height - 1 - Value
    };

    //ResolveY resolves using the world generation context, maps to vanilla resolveY(WorldGenerationContext)
    public int ResolveY(WorldGenerationContext context) => ResolveY(context.GetMinGenY(), context.GetGenDepth());

    //Codec single-key object codec, maps to vanilla VerticalAnchor.CODEC
    public static readonly Codec<VerticalAnchor> Codec = new VerticalAnchorCodec();

    public override string ToString() => Kind switch
    {
        AnchorKind.Absolute => $"{Value} absolute",
        AnchorKind.AboveBottom => $"{Value} above bottom",
        _ => $"{Value} below top"
    };
}

//VerticalAnchorCodec single-key object codec
//Tries the three options in vanilla xor codec order absolute/above_bottom/below_top
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

    //ReadValue reads a single-key integer value; a missing field returns null and a non-number returns null, letting the caller report the error uniformly
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
