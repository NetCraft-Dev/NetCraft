using NetCraft.Codec;
using NetCraft.Game.World.Items;
using NetCraft.Network;

namespace NetCraft.Game.World.Items.Component;

//ItemContainerContents item container contents, at most 256 slots, only non-empty slots are stored
//Maps to vanilla net.minecraft.world.item.component.ItemContainerContents
public sealed class ItemContainerContents : IEquatable<ItemContainerContents>
{
    //Slots slot limit, maps to vanilla SLOTS
    public const int Slots = 256;

    //Empty empty container, maps to vanilla EMPTY
    public static readonly ItemContainerContents Empty = new(Array.Empty<ItemStackTemplate>());

    //Codec persistence codec, the payload is a list of non-empty templates and exceeding the slot limit errors out, maps to vanilla CODEC
    public static readonly Codec<ItemContainerContents> Codec = ItemStackTemplate.PersistentCodec.ListOf().ComapFlatMap(
        items => items.Count > Slots
            ? DataResult<ItemContainerContents>.Error(() => $"container contents exceed {Slots} entries")
            : DataResult<ItemContainerContents>.Success(new ItemContainerContents(items)),
        contents => contents.Items);

    //StreamCodec network codec, writes only non-empty entries, maps to vanilla STREAM_CODEC
    public static readonly StreamCodec<RegistryFriendlyByteBuf, ItemContainerContents> StreamCodec =
        new ItemContainerContentsStreamCodec();

    public ItemContainerContents(IReadOnlyList<ItemStackTemplate> items) => Items = items;

    public IReadOnlyList<ItemStackTemplate> Items { get; }

    //NonEmptyItems materializes into item stacks, maps to vanilla nonEmptyItems
    public IEnumerable<ItemStack> NonEmptyItems() => Items.Select(template => template.Create());

    public bool Equals(ItemContainerContents? other)
        => other is not null && Items.SequenceEqual(other.Items);

    public override bool Equals(object? obj) => Equals(obj as ItemContainerContents);

    public override int GetHashCode() => Items.Count;

    public override string ToString() => $"ItemContainerContents[{Items.Count} items]";
}

//ItemContainerContentsStreamCodec list of non-empty templates goes in and out, maps to vanilla STREAM_CODEC
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
