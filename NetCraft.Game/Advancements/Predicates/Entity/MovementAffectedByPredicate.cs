using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates.Entity;

//MovementAffectedByPredicate movement affected by predicate, checks the position of the block affecting the entity's movement
//maps to vanilla net.minecraft.advancements.predicates.entity.MovementAffectedByPredicate
public sealed record MovementAffectedByPredicate(LocationPredicate Location) : EntitySubPredicate
{
    //Codec persistence codec, maps to vanilla CODEC
    public static readonly Codec<MovementAffectedByPredicate> Codec = LocationPredicate.Codec.ComapFlatMap(
        location => DataResult<MovementAffectedByPredicate>.Success(new MovementAffectedByPredicate(location)),
        predicate => predicate.Location);

    //Matches takes the center coordinate of the block underfoot that affects movement and hands it to the location predicate; fails when there is no level, maps to vanilla matches
    public bool Matches(NetCraft.Registry.Entity entity, ILevelReader? level, Vec3? position)
    {
        if (level is null) return false;
        var onPos = entity.GetBlockPosBelowThatAffectsMyMovement();
        return Location.Matches(level, onPos.X + 0.5, onPos.Y + 0.5, onPos.Z + 0.5);
    }
}
