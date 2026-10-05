using NetCraft.Codec;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates.Entity;

//MovementPredicate 移动谓词 判定实体的本刻位移速度与坠落距离
//对应原版 net.minecraft.advancements.predicates.entity.MovementPredicate
public sealed record MovementPredicate(
    MinMaxBounds.Doubles X,
    MinMaxBounds.Doubles Y,
    MinMaxBounds.Doubles Z,
    MinMaxBounds.Doubles Speed,
    MinMaxBounds.Doubles HorizontalSpeed,
    MinMaxBounds.Doubles VerticalSpeed,
    MinMaxBounds.Doubles FallDistance) : EntitySubPredicate
{
    //Codec 持久化编解码 字段名 x y z speed horizontal_speed vertical_speed fall_distance 对应原版 CODEC
    public static readonly Codec<MovementPredicate> Codec = RecordCodecBuilder.Of7(
        MinMaxBounds.Doubles.CODEC.OptionalFieldOf("x", MinMaxBounds.Doubles.Any)
            .ForGetter((MovementPredicate predicate) => predicate.X),
        MinMaxBounds.Doubles.CODEC.OptionalFieldOf("y", MinMaxBounds.Doubles.Any)
            .ForGetter((MovementPredicate predicate) => predicate.Y),
        MinMaxBounds.Doubles.CODEC.OptionalFieldOf("z", MinMaxBounds.Doubles.Any)
            .ForGetter((MovementPredicate predicate) => predicate.Z),
        MinMaxBounds.Doubles.CODEC.OptionalFieldOf("speed", MinMaxBounds.Doubles.Any)
            .ForGetter((MovementPredicate predicate) => predicate.Speed),
        MinMaxBounds.Doubles.CODEC.OptionalFieldOf("horizontal_speed", MinMaxBounds.Doubles.Any)
            .ForGetter((MovementPredicate predicate) => predicate.HorizontalSpeed),
        MinMaxBounds.Doubles.CODEC.OptionalFieldOf("vertical_speed", MinMaxBounds.Doubles.Any)
            .ForGetter((MovementPredicate predicate) => predicate.VerticalSpeed),
        MinMaxBounds.Doubles.CODEC.OptionalFieldOf("fall_distance", MinMaxBounds.Doubles.Any)
            .ForGetter((MovementPredicate predicate) => predicate.FallDistance),
        (x, y, z, speed, horizontalSpeed, verticalSpeed, fallDistance) =>
            new MovementPredicate(x, y, z, speed, horizontalSpeed, verticalSpeed, fallDistance));

    //SpeedOnly 只约束整体速度 对应原版 speed
    public static MovementPredicate SpeedOnly(MinMaxBounds.Doubles bounds)
        => new(MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any,
            bounds, MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any);

    //HorizontalSpeedOnly 只约束水平速度 对应原版 horizontalSpeed
    public static MovementPredicate HorizontalSpeedOnly(MinMaxBounds.Doubles bounds)
        => new(MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any,
            MinMaxBounds.Doubles.Any, bounds, MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any);

    //VerticalSpeedOnly 只约束竖直速度 对应原版 verticalSpeed
    public static MovementPredicate VerticalSpeedOnly(MinMaxBounds.Doubles bounds)
        => new(MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any,
            MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any, bounds, MinMaxBounds.Doubles.Any);

    //FallDistanceOnly 只约束坠落距离 对应原版 fallDistance
    public static MovementPredicate FallDistanceOnly(MinMaxBounds.Doubles bounds)
        => new(MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any,
            MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any, bounds);

    //Matches 各轴位移逐项判定 整体与水平速度按平方比对 对应原版 matches
    public bool Matches(double x, double y, double z, double fallDistance)
    {
        if (!X.Matches(x) || !Y.Matches(y) || !Z.Matches(z)) return false;
        if (!Speed.MatchesSqr(x * x + y * y + z * z)) return false;
        if (!HorizontalSpeed.MatchesSqr(x * x + z * z)) return false;
        return VerticalSpeed.Matches(Math.Abs(y)) && FallDistance.Matches(fallDistance);
    }

    //Matches 本刻位移取 Pos 与上一刻之差再乘 20 换成每秒速度 对应原版 matches
    public bool Matches(NetCraft.Registry.Entity entity, Vec3? position)
    {
        var velocity = entity.Pos.Subtract(entity.PreviousPos).Multiply(20.0);
        return Matches(velocity.X, velocity.Y, velocity.Z, entity.FallDistance);
    }
}
