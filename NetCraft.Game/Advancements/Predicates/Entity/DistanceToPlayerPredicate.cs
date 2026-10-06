using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates.Entity;

//DistanceToPlayerPredicate distance-to-initiator predicate, checks the entity's distance to the reference position on each axis
//maps to vanilla net.minecraft.advancements.predicates.entity.DistanceToPlayerPredicate
public sealed record DistanceToPlayerPredicate(DistancePredicate Distance) : EntitySubPredicate
{
    //Codec persistence codec, maps to vanilla CODEC
    public static readonly Codec<DistanceToPlayerPredicate> Codec = DistancePredicate.Codec.ComapFlatMap(
        distance => DataResult<DistanceToPlayerPredicate>.Success(new DistanceToPlayerPredicate(distance)),
        predicate => predicate.Distance);

    //Matches the reference position and entity position are compared per axis; fails when the reference position is missing, maps to vanilla matches
    public bool Matches(NetCraft.Registry.Entity entity, ILevelReader? level, Vec3? position)
        => position is { } origin
            && Distance.Matches(origin.X, origin.Y, origin.Z, entity.Pos.X, entity.Pos.Y, entity.Pos.Z);
}
