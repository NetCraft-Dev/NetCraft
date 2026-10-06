using NetCraft.Codec;
using NetCraft.Game.World.Entity;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates.Entity;

//EntityEffectsPredicate entity effects predicate, checks whether the mob effects on the entity satisfy the expectations
//maps to vanilla net.minecraft.advancements.predicates.entity.EntityEffectsPredicate
public sealed record EntityEffectsPredicate(MobEffectsPredicate Effects) : EntitySubPredicate
{
    //Codec persistence codec, maps to vanilla CODEC
    public static readonly Codec<EntityEffectsPredicate> Codec = MobEffectsPredicate.Codec.ComapFlatMap(
        effects => DataResult<EntityEffectsPredicate>.Success(new EntityEffectsPredicate(effects)),
        predicate => predicate.Effects);

    //Matches a non-effect holder fails, otherwise the effect table is handed to the effects predicate, maps to vanilla matches
    public bool Matches(NetCraft.Registry.Entity entity, ILevelReader? level, Vec3? position)
        => entity is IEffectHolder holder && Effects.Matches(holder.Effects.Map);
}
