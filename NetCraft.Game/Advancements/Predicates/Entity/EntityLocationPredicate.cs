using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates.Entity;

//EntityLocationPredicate entity location predicate, checks the entity's own coordinate with the location predicate
//maps to vanilla net.minecraft.advancements.predicates.entity.EntityLocationPredicate
public sealed record EntityLocationPredicate(LocationPredicate Location) : EntitySubPredicate
{
    //Codec persistence codec, maps to vanilla CODEC
    public static readonly Codec<EntityLocationPredicate> Codec = LocationPredicate.Codec.ComapFlatMap(
        location => DataResult<EntityLocationPredicate>.Success(new EntityLocationPredicate(location)),
        predicate => predicate.Location);

    //Matches the entity's own coordinate is handed to the location predicate; fails when there is no level, maps to vanilla matches
    public bool Matches(NetCraft.Registry.Entity entity, ILevelReader? level, Vec3? position)
        => level is not null && Location.Matches(level, entity.Pos.X, entity.Pos.Y, entity.Pos.Z);
}
