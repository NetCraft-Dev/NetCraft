using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates.Entity;

//SteppingOnPredicate stepping-on location predicate, checks the position of the supporting block under the entity
//maps to vanilla net.minecraft.advancements.predicates.entity.SteppingOnPredicate
public sealed record SteppingOnPredicate(LocationPredicate Location) : EntitySubPredicate
{
    //Codec persistence codec, maps to vanilla CODEC
    public static readonly Codec<SteppingOnPredicate> Codec = LocationPredicate.Codec.ComapFlatMap(
        location => DataResult<SteppingOnPredicate>.Success(new SteppingOnPredicate(location)),
        predicate => predicate.Location);

    //Matches fails when not on ground or there is no level; otherwise takes the center coordinate of the supporting block underfoot and hands it to the location predicate, maps to vanilla matches
    public bool Matches(NetCraft.Registry.Entity entity, ILevelReader? level, Vec3? position)
    {
        if (!entity.OnGround || level is null) return false;
        var onPos = entity.GetOnPos();
        return Location.Matches(level, onPos.X + 0.5, onPos.Y + 0.5, onPos.Z + 0.5);
    }
}
