using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates.Entity;

//EntityTypePredicate entity type predicate, checks whether the entity type falls in the given set
//maps to vanilla net.minecraft.advancements.predicates.entity.EntityTypePredicate
public sealed record EntityTypePredicate(HolderSet<EntityType<object>> Types) : EntitySubPredicate
{
    //Codec persistence codec, accepts a single id or an id list or #tag, maps to vanilla CODEC
    public static readonly Codec<EntityTypePredicate> Codec = HolderSetCodecs.EntityTypeSet.ComapFlatMap(
        types => DataResult<EntityTypePredicate>.Success(new EntityTypePredicate(types)),
        predicate => predicate.Types);

    //Matches whether the type is in the set, maps to vanilla matches
    public bool Matches(Holder<EntityType<object>> type) => Types.Contains(type);

    //Matches fetches the entity type then compares, maps to vanilla matches
    public bool Matches(NetCraft.Registry.Entity entity, ILevelReader? level, Vec3? position)
        => entity.Type is { } type && Matches(BuiltInRegistries.ENTITY_TYPE.WrapAsHolder(type));
}
