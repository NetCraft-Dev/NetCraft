using NetCraft.Codec;
using NetCraft.Game.World.Items;
using NetCraft.Network;

namespace NetCraft.Game.World.Items.Component;

//ItemContainerContents 物品容器内容 最多 256 槽 只登记非空槽
//对应原版 net.minecraft.world.item.component.ItemContainerContents
public sealed class ItemContainerContents : IEquatable<ItemContainerContents>
{
    //Slots 槽位上限 对应原版 SLOTS
    public const int Slots = 256;

    //Empty 空容器 对应原版 EMPTY
    public static readonly ItemContainerContents Empty = new(Array.Empty<ItemStackTemplate>());

    //Codec 持久化编解码 本体是非空模板列表 超出槽位上限直接报错 对应原版 CODEC
    public static readonly Codec<ItemContainerContents> Codec = ItemStackTemplate.PersistentCodec.ListOf().ComapFlatMap(
        items => items.Count > Slots
            ? DataResult<ItemContainerContents>.Error(() => $"容器内容超过 {Slots} 项")
            : DataResult<ItemContainerContents>.Success(new ItemContainerContents(items)),
        contents => contents.Items);

    //StreamCodec 网络编解码 只写非空项 对应原版 STREAM_CODEC
    public static readonly StreamCodec<RegistryFriendlyByteBuf, ItemContainerContents> StreamCodec =
        new ItemContainerContentsStreamCodec();

    public ItemContainerContents(IReadOnlyList<ItemStackTemplate> items) => Items = items;

    public IReadOnlyList<ItemStackTemplate> Items { get; }

    //NonEmptyItems 物化成物品栈 对应原版 nonEmptyItems
    public IEnumerable<ItemStack> NonEmptyItems() => Items.Select(template => template.Create());

    public bool Equals(ItemContainerContents? other)
        => other is not null && Items.SequenceEqual(other.Items);

    public override bool Equals(object? obj) => Equals(obj as ItemContainerContents);

    public override int GetHashCode() => Items.Count;

    public override string ToString() => $"ItemContainerContents[{Items.Count} items]";
}

//ItemContainerContentsStreamCodec 非空模板列表进出 对应原版 STREAM_CODEC
internal sealed class ItemContainerContentsStreamCodec : StreamCodec<RegistryFriendlyByteBuf, ItemContainerContents>
{
    public ItemContainerContents Decode(RegistryFriendlyByteBuf buf)
    {
        var size = buf.ReadVarInt();
        var items = new List<ItemStackTemplate>(Math.Min(size, ByteBufCodecs.MaxInitialCollectionSize));
        for (var i = 0; i < size; i++) items.Add(ItemStackTemplate.StreamCodec.Decode(buf));
        return new ItemContainerContents(items);
    }

    public void Encode(RegistryFriendlyByteBuf buf, ItemContainerContents value)
    {
        buf.WriteVarInt(value.Items.Count);
        foreach (var item in value.Items) ItemStackTemplate.StreamCodec.Encode(buf, item);
    }
}
