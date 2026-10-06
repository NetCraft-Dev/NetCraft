using NetCraft.Codec;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates.Entity;

//MovementPredicate movement predicate, checks the entity's per-tick displacement speed and fall distance
//maps to vanilla net.minecraft.advancements.predicates.entity.MovementPredicate
public sealed record MovementPredicate(
    MinMaxBounds.Doubles X,
    MinMaxBounds.Doubles Y,
    MinMaxBounds.Doubles Z,
    MinMaxBounds.Doubles Speed,
    MinMaxBounds.Doubles HorizontalSpeed,
    MinMaxBounds.Doubles VerticalSpeed,
    MinMaxBounds.Doubles FallDistance) : EntitySubPredicate
{
    //Codec persistence codec, field names x y z speed horizontal_speed vertical_speed fall_distance, maps to vanilla CODEC
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

    //SpeedOnly constrains only the overall speed, maps to vanilla speed
    public static MovementPredicate SpeedOnly(MinMaxBounds.Doubles bounds)
        => new(MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any,
            bounds, MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any);

    //HorizontalSpeedOnly constrains only the horizontal speed, maps to vanilla horizontalSpeed
    public static MovementPredicate HorizontalSpeedOnly(MinMaxBounds.Doubles bounds)
        => new(MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any,
            MinMaxBounds.Doubles.Any, bounds, MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any);

    //VerticalSpeedOnly constrains only the vertical speed, maps to vanilla verticalSpeed
    public static MovementPredicate VerticalSpeedOnly(MinMaxBounds.Doubles bounds)
        => new(MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any,
            MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any, bounds, MinMaxBounds.Doubles.Any);

    //FallDistanceOnly constrains only the fall distance, maps to vanilla fallDistance
    public static MovementPredicate FallDistanceOnly(MinMaxBounds.Doubles bounds)
        => new(MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any,
            MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any, MinMaxBounds.Doubles.Any, bounds);

    //Matches per-axis displacement checked item by item; overall and horizontal speeds compare by squared value, maps to vanilla matches
    public bool Matches(double x, double y, double z, double fallDistance)
    {
        if (!X.Matches(x) || !Y.Matches(y) || !Z.Matches(z)) return false;
        if (!Speed.MatchesSqr(x * x + y * y + z * z)) return false;
        if (!HorizontalSpeed.MatchesSqr(x * x + z * z)) return false;
        return VerticalSpeed.Matches(Math.Abs(y)) && FallDistance.Matches(fallDistance);
    }

    //Matches per-tick displacement is the difference between Pos and the previous tick times 20 to convert to per-second speed, maps to vanilla matches
    public bool Matches(NetCraft.Registry.Entity entity, ILevelReader? level, Vec3? position)
    {
        var velocity = entity.Pos.Subtract(entity.PreviousPos).Multiply(20.0);
        return Matches(velocity.X, velocity.Y, velocity.Z, entity.FallDistance);
    }
}
