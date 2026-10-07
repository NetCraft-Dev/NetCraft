using NetCraft.Network;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Inventory;

//HashedStack hashed stack, maps to vanilla net.minecraft.network.HashedStack
//Stack digest reported in the client click packet, the server would validate the client prediction with it, this project only decodes without validating
//Encoding maps to vanilla ByteBufCodecs.optional(ActualItem.STREAM_CODEC), an empty stack writes a single false
public abstract class HashedStack
{
    //Empty empty stack singleton
    public static readonly HashedStack Empty = new EmptyStack();

    //StreamCodec network codec
    public static readonly StreamCodec<RegistryFriendlyByteBuf, HashedStack> StreamCodec
        = new HashedStackCodec();

    //ActualItem non-empty stack digest: item holder + count + component hash
    public sealed class ActualItem : HashedStack
    {
        public ActualItem(Holder<Item> item, int count, HashedPatchMap components)
        {
            Item = item;
            Count = count;
            Components = components;
        }

        //Item item holder, maps to vanilla item
        public Holder<Item> Item { get; }

        //Count count, maps to vanilla count
        public int Count { get; }

        //Components component hash, maps to vanilla components
        public HashedPatchMap Components { get; }
    }

    private sealed class EmptyStack : HashedStack { }
}

//HashedStackCodec writes false for an empty stack and true plus item holder, count and component hash otherwise
internal sealed class HashedStackCodec : StreamCodec<RegistryFriendlyByteBuf, HashedStack>
{
    public HashedStack Decode(RegistryFriendlyByteBuf buf)
    {
        if (!buf.ReadBoolean())
            return HashedStack.Empty;
        var item = ItemCodecs.StreamCodec.Decode(buf);
        int count = buf.ReadVarInt();
        var components = HashedPatchMap.StreamCodec.Decode(buf);
        return new HashedStack.ActualItem(item, count, components);
    }

    public void Encode(RegistryFriendlyByteBuf buf, HashedStack value)
    {
        if (value is not HashedStack.ActualItem actual)
        {
            buf.WriteBoolean(false);
            return;
        }
        buf.WriteBoolean(true);
        ItemCodecs.StreamCodec.Encode(buf, actual.Item);
        buf.WriteVarInt(actual.Count);
        HashedPatchMap.StreamCodec.Encode(buf, actual.Components);
    }
}

//HashedPatchMap component patch hash, maps to vanilla net.minecraft.network.HashedPatchMap
//addedComponents maps added component types to their value hashes, removedComponents records the removed component types
public sealed class HashedPatchMap
{
    //MaxComponents per-entry count limit, matches vanilla's 256
    internal const int MaxComponents = 256;

    //StreamCodec network codec
    public static readonly StreamCodec<RegistryFriendlyByteBuf, HashedPatchMap> StreamCodec
        = new HashedPatchMapCodec();

    public HashedPatchMap(Dictionary<object, int> addedComponents, HashSet<object> removedComponents)
    {
        AddedComponents = addedComponents;
        RemovedComponents = removedComponents;
    }

    //AddedComponents maps added component types to value hashes
    public IReadOnlyDictionary<object, int> AddedComponents { get; }

    //RemovedComponents removed component types
    public IReadOnlySet<object> RemovedComponents { get; }
}

//HashedPatchMapCodec writes added entries then removed entries, each prefixed by a VarInt count
internal sealed class HashedPatchMapCodec : StreamCodec<RegistryFriendlyByteBuf, HashedPatchMap>
{
    public HashedPatchMap Decode(RegistryFriendlyByteBuf buf)
    {
        int addedCount = buf.ReadVarInt();
        if (addedCount > HashedPatchMap.MaxComponents)
            throw new InvalidOperationException($"component hash added entries out of range: {addedCount}");
        var added = new Dictionary<object, int>(Math.Min(addedCount, ByteBufCodecs.MaxInitialCollectionSize));
        for (int i = 0; i < addedCount; i++)
        {
            var type = DataComponentTypeCodecs.Decode(buf);
            added[type] = buf.ReadVarInt();
        }

        int removedCount = buf.ReadVarInt();
        if (removedCount > HashedPatchMap.MaxComponents)
            throw new InvalidOperationException($"component hash removed entries out of range: {removedCount}");
        var removed = new HashSet<object>();
        for (int i = 0; i < removedCount; i++)
            removed.Add(DataComponentTypeCodecs.Decode(buf));

        return new HashedPatchMap(added, removed);
    }

    public void Encode(RegistryFriendlyByteBuf buf, HashedPatchMap value)
    {
        buf.WriteVarInt(value.AddedComponents.Count);
        foreach (var entry in value.AddedComponents)
        {
            DataComponentTypeCodecs.Encode(buf, entry.Key);
            buf.WriteVarInt(entry.Value);
        }

        buf.WriteVarInt(value.RemovedComponents.Count);
        foreach (var type in value.RemovedComponents)
            DataComponentTypeCodecs.Encode(buf, type);
    }
}
