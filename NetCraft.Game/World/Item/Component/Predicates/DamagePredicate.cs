using NetCraft.Codec;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component.Predicates;

//DamagePredicate durability and damage predicate, maps to vanilla net.minecraft.core.component.predicates.DamagePredicate
//Durability is the max damage minus the current damage, both ranges must hold to match
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
        //Without a damage component there is no durability, so it does not match, maps to the vanilla damage == null branch
        if (components.Get(DataComponents.DAMAGE) is not int damage) return false;
        var maxDamage = components.GetOrDefault(DataComponents.MAX_DAMAGE, 0) is int value ? value : 0;
        return Durability.Matches(maxDamage - damage) && Damage.Matches(damage);
    }
}
