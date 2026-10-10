using NetCraft.Codec;
using NetCraft.Network;
using NetCraft.Game.World.Items.Component;
using NetCraft.Registry;
using NetCraft.Util;

namespace NetCraft.Game.World.Items;

//ItemStackTemplate item stack template, maps to vanilla net.minecraft.world.item.ItemStackTemplate
//Stores only the item, count and component patch, Create materializes an ItemStack when it actually needs to go on the wire
public sealed record ItemStackTemplate(Holder<Item> Item, int Count, DataComponentPatch Components) : ItemInstance
{
    //PersistentCodec persistence codec, covers the vanilla MAP_CODEC and CODEC forms
    //Decodes the field group first, falling back to the item-name-only shorthand when that fails
    public static readonly Codec<ItemStackTemplate> PersistentCodec = Codecs.WithAlternative(
        RecordCodecBuilder.Of3(
            NetCraft.Registry.Item.CODEC.FieldOf("id").ForGetter((ItemStackTemplate template) => template.Item),
            ExtraCodecs.IntRange(1, 99).OptionalFieldOf("count", 1).ForGetter((ItemStackTemplate template) => template.Count),
            DataComponentPatch.PersistentCodec.OptionalFieldOf("components", DataComponentPatch.Empty).ForGetter((ItemStackTemplate template) => template.Components),
            (item, count, components) => new ItemStackTemplate(item, count, components)),
        NetCraft.Registry.Item.CODEC.ComapFlatMap(
            holder => DataResult<ItemStackTemplate>.Success(new ItemStackTemplate(holder, 1, DataComponentPatch.Empty)),
            template => template.Item));

    public static readonly StreamCodec<RegistryFriendlyByteBuf, ItemStackTemplate> StreamCodec = new ItemStackTemplateStreamCodec();

    public Holder<Item> TypeHolder => Item;

    //FromStack takes a template from a non-empty stack
    public static ItemStackTemplate FromStack(ItemStack stack) => new(stack.GetTypeHolder()!, stack.GetCount(), stack.GetComponents().AsPatch());

    //FromNonEmptyStack rejects empty stacks outright
    public static ItemStackTemplate FromNonEmptyStack(ItemStack stack)
    {
        if (stack.IsEmpty()) throw new InvalidOperationException("Stack must be non-empty");
        return FromStack(stack);
    }

    public ItemStackTemplate WithCount(int count) => Count == count ? this : new ItemStackTemplate(Item, count, Components);

    //Create materializes an item stack; vanilla also runs a strict validation pass, not yet wired up
    public ItemStack Create() => new(Item, Count, Components);

    //Get checks the stack's own patch first, then falls back to the item's built-in components
    public T? Get<T>(DataComponentType<T> type) where T : class
    {
        var patched = Components.Get(type);
        if (patched is { } optional) return optional.IsPresent ? optional.Get() as T : null;
        return Item.Components.Get(type);
    }
}

//ItemStackTemplateStreamCodec maps to vanilla STREAM_CODEC, writes item, count and component patch in order
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
