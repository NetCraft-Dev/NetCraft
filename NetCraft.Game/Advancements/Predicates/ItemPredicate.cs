using NetCraft.Codec;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.World.Items;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates;

//ItemPredicate 物品谓词 物品集合加堆叠数量加组件匹配 对应原版 net.minecraft.advancements.predicates.ItemPredicate
public sealed record ItemPredicate(
    Optional<HolderSet<Item>> Items,
    MinMaxBounds.Ints Count,
    DataComponentMatchers Components) : IValuePredicate<ItemStack>
{
    //Codec 持久化编解码 字段名 items 与 count 对应原版 CODEC
    public static readonly Codec<ItemPredicate> Codec = RecordCodecBuilder.Of3(
        HolderSetCodecs.ItemSet.OptionalFieldOf("items")
            .ForGetter((ItemPredicate predicate) => predicate.Items),
        MinMaxBounds.Ints.CODEC.OptionalFieldOf(ItemInstance.FieldCount, MinMaxBounds.Ints.Any)
            .ForGetter((ItemPredicate predicate) => predicate.Count),
        DataComponentMatchers.Codec.ForGetter((ItemPredicate predicate) => predicate.Components),
        (items, count, components) => new ItemPredicate(items, count, components));

    //Test 物品栈是否满足物品类型与数量与组件三重条件
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
