using NetCraft.Codec;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component.Predicates;

//DamagePredicate 耐久与损坏值谓词 对应原版 net.minecraft.core.component.predicates.DamagePredicate
//耐久按最大耐久减当前损坏值算 两项区间都满足才算匹配
public sealed record DamagePredicate(MinMaxBounds.Ints Durability, MinMaxBounds.Ints Damage) : DataComponentPredicate
{
    public static readonly Codec<DamagePredicate> Codec = RecordCodecBuilder.Of2(
        MinMaxBounds.Ints.CODEC.OptionalFieldOf("durability", MinMaxBounds.Ints.Any)
            .ForGetter((DamagePredicate predicate) => predicate.Durability),
        MinMaxBounds.Ints.CODEC.OptionalFieldOf("damage", MinMaxBounds.Ints.Any)
            .ForGetter((DamagePredicate predicate) => predicate.Damage),
        (durability, damage) => new DamagePredicate(durability, damage));

    public bool Matches(DataComponentGetter components)
    {
        //没有损坏值组件就谈不上耐久 直接不匹配 对应原版 damage == null 分支
        if (components.Get(DataComponents.DAMAGE) is not int damage) return false;
        var maxDamage = components.GetOrDefault(DataComponents.MAX_DAMAGE, 0) is int value ? value : 0;
        return Durability.Matches(maxDamage - damage) && Damage.Matches(damage);
    }
}
