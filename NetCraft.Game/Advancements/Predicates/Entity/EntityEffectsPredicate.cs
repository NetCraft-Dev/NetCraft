using NetCraft.Codec;
using NetCraft.Game.World.Entity;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates.Entity;

//EntityEffectsPredicate 实体效果谓词 判定实体身上的药水效果是否满足期望
//对应原版 net.minecraft.advancements.predicates.entity.EntityEffectsPredicate
public sealed record EntityEffectsPredicate(MobEffectsPredicate Effects) : EntitySubPredicate
{
    //Codec 持久化编解码 对应原版 CODEC
    public static readonly Codec<EntityEffectsPredicate> Codec = MobEffectsPredicate.Codec.ComapFlatMap(
        effects => DataResult<EntityEffectsPredicate>.Success(new EntityEffectsPredicate(effects)),
        predicate => predicate.Effects);

    //Matches 非效果持有者判否 否则把效果表交给效果谓词 对应原版 matches
    public bool Matches(NetCraft.Registry.Entity entity, ILevelReader? level, Vec3? position)
        => entity is IEffectHolder holder && Effects.Matches(holder.Effects.Map);
}
