using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates.Entity;

//PeriodicEntityTickPredicate periodic tick predicate, passes only when the entity's living ticks modulo the period is zero
//maps to vanilla net.minecraft.advancements.predicates.entity.PeriodicEntityTickPredicate
public sealed record PeriodicEntityTickPredicate(int PeriodicTick) : EntitySubPredicate
{
    //Codec persistence codec, only accepts positive integers, maps to vanilla ExtraCodecs.POSITIVE_INT
    public static readonly Codec<PeriodicEntityTickPredicate> Codec = Codecs.Int.ComapFlatMap(
        value => value > 0
            ? DataResult<PeriodicEntityTickPredicate>.Success(new PeriodicEntityTickPredicate(value))
            : DataResult<PeriodicEntityTickPredicate>.Error(() => "periodic tick count must be a positive integer"),
        predicate => predicate.PeriodicTick);

    //Matches passes when the living ticks are divisible by the period, maps to vanilla matches
    public bool Matches(NetCraft.Registry.Entity entity, ILevelReader? level, Vec3? position)
        => entity.TickCount % PeriodicTick == 0;
}
