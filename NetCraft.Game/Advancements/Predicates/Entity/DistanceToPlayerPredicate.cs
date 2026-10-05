using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates.Entity;

//DistanceToPlayerPredicate 与发起者距离谓词 判定实体与参照位置在各轴向上的距离
//对应原版 net.minecraft.advancements.predicates.entity.DistanceToPlayerPredicate
public sealed record DistanceToPlayerPredicate(DistancePredicate Distance) : EntitySubPredicate
{
    //Codec 持久化编解码 对应原版 CODEC
    public static readonly Codec<DistanceToPlayerPredicate> Codec = DistancePredicate.Codec.ComapFlatMap(
        distance => DataResult<DistanceToPlayerPredicate>.Success(new DistanceToPlayerPredicate(distance)),
        predicate => predicate.Distance);

    //Matches 参照位置与实体位置逐轴比对 缺少参照位置时判否 对应原版 matches
    public bool Matches(NetCraft.Registry.Entity entity, Vec3? position)
        => position is { } origin
            && Distance.Matches(origin.X, origin.Y, origin.Z, entity.Pos.X, entity.Pos.Y, entity.Pos.Z);
}
