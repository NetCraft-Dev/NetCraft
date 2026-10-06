using NetCraft.Codec;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.World.Items;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates;

//ItemPredicate item predicate, item set plus stack count plus component matching, maps to vanilla net.minecraft.advancements.predicates.ItemPredicate
public sealed record ItemPredicate(
    Optional<HolderSet<Item>> Items,
    MinMaxBounds.Ints Count,
    DataComponentMatchers Components) : IValuePredicate<ItemStack>
{
    //Codec persistence codec, field names items/count, maps to vanilla CODEC
    public static readonly Codec<ItemPredicate> Codec = RecordCodecBuilder.Of3(
        HolderSetCodecs.ItemSet.OptionalFieldOf("items")
            .ForGetter((ItemPredicate predicate) => predicate.Items),
        MinMaxBounds.Ints.CODEC.OptionalFieldOf(ItemInstance.FieldCount, MinMaxBounds.Ints.Any)
            .ForGetter((ItemPredicate predicate) => predicate.Count),
        DataComponentMatchers.Codec.ForGetter((ItemPredicate predicate) => predicate.Components),
        (items, count, components) => new ItemPredicate(items, count, components));

    //Test whether the item stack satisfies the item type, count and component conditions
    public bool Test(ItemStack stack)
    {
        if (Items.IsPresent)
        {
            var holder = stack.GetTypeHolder();
            if (holder is null || !Items.Get().Contains(holder)) return false;
        }
        return Count.Matches(stack.GetCount()) && Components.Test(stack);
    }
}
