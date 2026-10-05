using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates.Entity;

//EntityTypePredicate 实体类型谓词 判定实体类型是否落在给定集合
//对应原版 net.minecraft.advancements.predicates.entity.EntityTypePredicate
public sealed record EntityTypePredicate(HolderSet<EntityType<object>> Types) : EntitySubPredicate
{
    //Codec 持久化编解码 接受单个 id 或 id 列表或 #标签 对应原版 CODEC
    public static readonly Codec<EntityTypePredicate> Codec = HolderSetCodecs.EntityTypeSet.ComapFlatMap(
        types => DataResult<EntityTypePredicate>.Success(new EntityTypePredicate(types)),
        predicate => predicate.Types);

    //Matches 类型是否在集合里 对应原版 matches
    public bool Matches(Holder<EntityType<object>> type) => Types.Contains(type);

    //Matches 取实体类型后比对 对应原版 matches
    public bool Matches(NetCraft.Registry.Entity entity, Vec3? position)
        => entity.Type is { } type && Matches(BuiltInRegistries.ENTITY_TYPE.WrapAsHolder(type));
}
