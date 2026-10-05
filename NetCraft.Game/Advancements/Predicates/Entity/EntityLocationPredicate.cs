using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates.Entity;

//EntityLocationPredicate 实体所在位置谓词 用位置谓词判定实体自身坐标
//对应原版 net.minecraft.advancements.predicates.entity.EntityLocationPredicate
public sealed record EntityLocationPredicate(LocationPredicate Location) : EntitySubPredicate
{
    //Codec 持久化编解码 对应原版 CODEC
    public static readonly Codec<EntityLocationPredicate> Codec = LocationPredicate.Codec.ComapFlatMap(
        location => DataResult<EntityLocationPredicate>.Success(new EntityLocationPredicate(location)),
        predicate => predicate.Location);

    //Matches 实体自身坐标交给位置谓词 缺关卡时判否 对应原版 matches
    public bool Matches(NetCraft.Registry.Entity entity, ILevelReader? level, Vec3? position)
        => level is not null && Location.Matches(level, entity.Pos.X, entity.Pos.Y, entity.Pos.Z);
}
