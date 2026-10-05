using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates.Entity;

//PeriodicEntityTickPredicate 周期刻谓词 实体存活刻数按周期取模为零才通过
//对应原版 net.minecraft.advancements.predicates.entity.PeriodicEntityTickPredicate
public sealed record PeriodicEntityTickPredicate(int PeriodicTick) : EntitySubPredicate
{
    //Codec 持久化编解码 只接受正整数 对应原版 ExtraCodecs.POSITIVE_INT
    public static readonly Codec<PeriodicEntityTickPredicate> Codec = Codecs.Int.ComapFlatMap(
        value => value > 0
            ? DataResult<PeriodicEntityTickPredicate>.Success(new PeriodicEntityTickPredicate(value))
            : DataResult<PeriodicEntityTickPredicate>.Error(() => "周期刻数必须为正整数"),
        predicate => predicate.PeriodicTick);

    //Matches 存活刻数整除周期即通过 对应原版 matches
    public bool Matches(NetCraft.Registry.Entity entity, Vec3? position)
        => entity.TickCount % PeriodicTick == 0;
}
