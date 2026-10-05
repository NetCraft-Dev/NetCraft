using NetCraft.Codec;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Util;

namespace NetCraft.Game.Advancements.Predicates;

//DistancePredicate 距离谓词 判定两点在各轴向上是否落在给定区间
//对应原版 net.minecraft.advancements.predicates.DistancePredicate
public sealed record DistancePredicate(
    MinMaxBounds.Doubles X,
    MinMaxBounds.Doubles Y,
    MinMaxBounds.Doubles Z,
    MinMaxBounds.Doubles Horizontal,
    MinMaxBounds.Doubles Absolute)
{
    //Codec 持久化编解码 字段名 x y z horizontal absolute 对应原版 CODEC
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

    //HorizontalOnly 只约束水平距离 对应原版 horizontal
    public static DistancePredicate HorizontalOnly(MinMaxBounds.Doubles horizontal)
        => new(MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any,
            horizontal, MinMaxBounds.Doubles.Any);

    //VerticalOnly 只约束纵向距离 对应原版 vertical
    public static DistancePredicate VerticalOnly(MinMaxBounds.Doubles y)
        => new(MinMaxBounds.Doubles.Any, y, MinMaxBounds.Doubles.Any,
            MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any);

    //AbsoluteOnly 只约束直线距离 对应原版 absolute
    public static DistancePredicate AbsoluteOnly(MinMaxBounds.Doubles absolute)
        => new(MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any,
            MinMaxBounds.Doubles.Any, absolute);

    //Matches 两点距离逐项判定 水平与直线按平方值比对 对应原版 matches
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
