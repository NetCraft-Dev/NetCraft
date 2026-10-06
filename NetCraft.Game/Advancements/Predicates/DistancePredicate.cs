using NetCraft.Codec;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Util;

namespace NetCraft.Game.Advancements.Predicates;

//DistancePredicate distance predicate, checks whether two points fall in the given ranges on each axis
//maps to vanilla net.minecraft.advancements.predicates.DistancePredicate
public sealed record DistancePredicate(
    MinMaxBounds.Doubles X,
    MinMaxBounds.Doubles Y,
    MinMaxBounds.Doubles Z,
    MinMaxBounds.Doubles Horizontal,
    MinMaxBounds.Doubles Absolute)
{
    //Codec persistence codec, field names x/y/z/horizontal/absolute, maps to vanilla CODEC
    public static readonly Codec<DistancePredicate> Codec = RecordCodecBuilder.Of5(
        MinMaxBounds.Doubles.CODEC.OptionalFieldOf("x", MinMaxBounds.Doubles.Any)
            .ForGetter((DistancePredicate predicate) => predicate.X),
        MinMaxBounds.Doubles.CODEC.OptionalFieldOf("y", MinMaxBounds.Doubles.Any)
            .ForGetter((DistancePredicate predicate) => predicate.Y),
        MinMaxBounds.Doubles.CODEC.OptionalFieldOf("z", MinMaxBounds.Doubles.Any)
            .ForGetter((DistancePredicate predicate) => predicate.Z),
        MinMaxBounds.Doubles.CODEC.OptionalFieldOf("horizontal", MinMaxBounds.Doubles.Any)
            .ForGetter((DistancePredicate predicate) => predicate.Horizontal),
        MinMaxBounds.Doubles.CODEC.OptionalFieldOf("absolute", MinMaxBounds.Doubles.Any)
            .ForGetter((DistancePredicate predicate) => predicate.Absolute),
        (x, y, z, horizontal, absolute) => new DistancePredicate(x, y, z, horizontal, absolute));

    //HorizontalOnly constrains only the horizontal distance, maps to vanilla horizontal
    public static DistancePredicate HorizontalOnly(MinMaxBounds.Doubles horizontal)
        => new(MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any,
            horizontal, MinMaxBounds.Doubles.Any);

    //VerticalOnly constrains only the vertical distance, maps to vanilla vertical
    public static DistancePredicate VerticalOnly(MinMaxBounds.Doubles y)
        => new(MinMaxBounds.Doubles.Any, y, MinMaxBounds.Doubles.Any,
            MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any);

    //AbsoluteOnly constrains only the straight-line distance, maps to vanilla absolute
    public static DistancePredicate AbsoluteOnly(MinMaxBounds.Doubles absolute)
        => new(MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any,
            MinMaxBounds.Doubles.Any, absolute);

    //Matches two-point distance checked item by item; horizontal and straight-line compare by squared value, maps to vanilla matches
    public bool Matches(double x0, double y0, double z0, double x1, double y1, double z1)
    {
        var xd = (float)(x0 - x1);
        var yd = (float)(y0 - y1);
        var zd = (float)(z0 - z1);
        return X.Matches(Mth.Abs(xd))
            && Y.Matches(Mth.Abs(yd))
            && Z.Matches(Mth.Abs(zd))
            && Horizontal.MatchesSqr(xd * xd + zd * zd)
            && Absolute.MatchesSqr(xd * xd + yd * yd + zd * zd);
    }
}
