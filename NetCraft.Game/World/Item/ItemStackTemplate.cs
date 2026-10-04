using NetCraft.Network;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items;

//ItemStackTemplate 物品栈模板 对应原版 net.minecraft.world.item.ItemStackTemplate
//只存物品 数量 与组件补丁 需要真正入包时再 Create 出 ItemStack
public sealed record ItemStackTemplate(Holder<Item> Item, int Count, DataComponentPatch Components) : ItemInstance
{
    public static readonly StreamCodec<RegistryFriendlyByteBuf, ItemStackTemplate> StreamCodec = new ItemStackTemplateStreamCodec();

    public Holder<Item> TypeHolder => Item;

    //FromStack 从一个非空物品栈取模板
    public static ItemStackTemplate FromStack(ItemStack stack) => new(stack.GetTypeHolder()!, stack.GetCount(), stack.GetComponents().AsPatch());

    //FromNonEmptyStack 空栈直接拒绝
    public static ItemStackTemplate FromNonEmptyStack(ItemStack stack)
    {
        if (stack.IsEmpty()) throw new InvalidOperationException("Stack must be non-empty");
        return FromStack(stack);
    }

    public ItemStackTemplate WithCount(int count) => Count == count ? this : new ItemStackTemplate(Item, count, Components);

    //Create 物化成物品栈 原版还会走一遍严格校验 该能力尚未接入
    public ItemStack Create() => new(Item, Count, Components);

    //Get 先查自身补丁 再回退物品自带的组件
    public T? Get<T>(DataComponentType<T> type) where T : class
    {
        var patched = Components.Get(type);
        if (patched is { } optional) return optional.IsPresent ? optional.Get() as T : null;
        return Item.Components.Get(type);
    }
}

//ItemStackTemplateStreamCodec 对应原版 STREAM_CODEC 物品 数量 组件补丁依次写
internal sealed class ItemStackTemplateStreamCodec : StreamCodec<RegistryFriendlyByteBuf, ItemStackTemplate>
{
    public ItemStackTemplate Decode(RegistryFriendlyByteBuf buf)
    {
        var item = ItemCodecs.StreamCodec.Decode(buf);
        var count = buf.ReadVarInt();
        var components = DataComponentPatch.StreamCodec.Decode(buf);
        return new ItemStackTemplate(item, count, components);
    }

    public void Encode(RegistryFriendlyByteBuf buf, ItemStackTemplate value)
    {
        ItemCodecs.StreamCodec.Encode(buf, value.Item);
        buf.WriteVarInt(value.Count);
        DataComponentPatch.StreamCodec.Encode(buf, value.Components);
    }
}
