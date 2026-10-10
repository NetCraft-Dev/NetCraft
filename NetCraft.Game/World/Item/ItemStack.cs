using NetCraft.Nbt;
using NetCraft.Network;
using NetCraft.Game.World.Items.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items;

//ItemStack item stack, maps to vanilla net.minecraft.world.item.ItemStack
//Holds the triple of Holder<Item>, count and PatchedDataComponentMap
//STREAM_CODEC encodes count + Item.STREAM_CODEC + DataComponentPatch.STREAM_CODEC
//OPTIONAL_STREAM_CODEC allows empty stacks, count<=0 is treated as EMPTY
//STREAM_CODEC forbids empty stacks on top of OPTIONAL, encoding/decoding throws EncoderException/DecoderException
public sealed class ItemStack : ItemInstance, DataComponentHolder
{
    //Empty empty stack singleton, _item=null
    public static readonly ItemStack Empty = new();

    //OptionalStreamCodec allows empty stacks
    public static readonly StreamCodec<RegistryFriendlyByteBuf, ItemStack> OptionalStreamCodec
        = new ItemStackOptionalStreamCodec();

    //StreamCodec forbids empty stacks
    public static readonly StreamCodec<RegistryFriendlyByteBuf, ItemStack> StreamCodec
        = new ItemStackStreamCodec();

    private readonly Holder<Item>? _item;
    private int _count;
    private readonly PatchedDataComponentMap _components;

    private ItemStack()
    {
        _item = null;
        _count = 0;
        _components = new PatchedDataComponentMap(DataComponentMap.Empty, DataComponentPatch.Empty);
    }

    public ItemStack(Holder<Item> item, int count, DataComponentPatch patch)
    {
        _item = item;
        _count = count;
        _components = new PatchedDataComponentMap(item.Components, patch);
    }

    //GetCount item count
    public int GetCount() => _count;

    //Count count, ItemInstance read-only view
    public int Count => _count;

    //TypeHolder item type holder, ItemInstance read-only view, throws on an empty stack
    public Holder<Item> TypeHolder => _item ?? throw new InvalidOperationException("Cannot get item from empty ItemStack");

    //Get looks up a component of the stack, ItemInstance read-only view
    public T? Get<T>(DataComponentType<T> type) where T : class => _components.Get(type);

    //GetOrDefault looks up a component with a fallback when missing, maps to vanilla getOrDefault
    public T GetOrDefault<T>(DataComponentType<T> type, T fallback) where T : class => Get(type) ?? fallback;

    //Set overwrites a component, maps to vanilla set, empty stacks cannot be modified
    public void Set<T>(DataComponentType<T> type, T value) where T : class
    {
        if (IsEmpty()) throw new InvalidOperationException("Cannot modify an empty ItemStack");
        _components.Set(type, value);
    }

    //Remove removes a component, maps to vanilla remove
    public void Remove<T>(DataComponentType<T> type) where T : class
    {
        if (IsEmpty()) return;
        _components.Remove(type);
    }

    //SetCount sets the count
    public void SetCount(int count)
    {
        _count = count;
    }

    //Shrink reduces the count, clamped at 0, maps to vanilla shrink
    public void Shrink(int amount) => _count = Math.Max(0, _count - amount);

    //Split takes amount into a new stack and reduces this one, maps to vanilla split
    public ItemStack Split(int amount)
    {
        var taken = Math.Min(amount, _count);
        if (taken <= 0) return Empty;
        var result = CopyWithCount(taken);
        Shrink(taken);
        return result;
    }

    //GetMaxStackSize stack limit, 64 for an empty stack, maps to vanilla getMaxStackSize
    public int GetMaxStackSize() => IsEmpty() ? 64 : GetItem().GetDefaultMaxStackSize();

    //IsStackable whether the stack can still take part in stacking, maps to vanilla isStackable
    //Vanilla also requires undamaged durability; before the damage component lands only the stack limit is checked
    public bool IsStackable() => GetMaxStackSize() > 1;

    //IsSameItemAndComponentsAs same item and same components, maps to vanilla isSameItemSameComponents
    //Compares the component patch key by key; comparing only the patch reference would treat different enchantments as the same once the component system lands
    public bool IsSameItemAndComponentsAs(ItemStack other)
    {
        if (IsEmpty() || other.IsEmpty()) return false;
        if (!ReferenceEquals(_item!.Value, other._item!.Value)) return false;
        var mine = _components.AsPatch().AsMap();
        var theirs = other._components.AsPatch().AsMap();
        if (mine.Count != theirs.Count) return false;
        foreach (var (key, value) in mine)
        {
            if (!theirs.TryGetValue(key, out var otherValue)) return false;
            if (value.IsPresent != otherValue.IsPresent) return false;
            if (value.IsPresent && !Equals(value.Get(), otherValue.Get())) return false;
        }
        return true;
    }

    //IsEmpty whether the stack is empty
    public bool IsEmpty() => _item is null || _count <= 0;

    //GetItem returns the Item, throws on an empty stack
    public Item GetItem()
    {
        if (_item is null)
            throw new InvalidOperationException("Cannot get item from empty ItemStack");
        return _item.Value;
    }

    //GetTypeHolder returns Holder<Item>, null for an empty stack
    public Holder<Item>? GetTypeHolder() => _item;

    //GetComponents returns the component map
    public PatchedDataComponentMap GetComponents() => _components;

    //Explicit implementation of DataComponentHolder exposing the same map under the base interface type
    DataComponentMap DataComponentHolder.GetComponents() => _components;

    //Copy copies the item stack
    public ItemStack Copy()
    {
        if (IsEmpty()) return Empty;
        return new ItemStack(_item!, _count, _components.AsPatch());
    }

    //CopyWithCount copies with the given count
    public ItemStack CopyWithCount(int count)
    {
        if (IsEmpty()) return Empty;
        return new ItemStack(_item!, count, _components.AsPatch());
    }

    //WriteNbt writes the stack in the vanilla 1.20.5+ id + count + components shape, empty stacks write nothing
    public static void WriteNbt(CompoundTag parent, string key, ItemStack stack)
    {
        if (ToNbt(stack) is { } entry) parent.Put(key, entry);
    }

    //ToNbt writes the stack tag, components land in the components field as a patch, returns null for an empty stack
    public static CompoundTag? ToNbt(ItemStack stack)
    {
        if (stack.IsEmpty()) return null;
        var entry = new CompoundTag();
        entry.PutString("id", stack.GetItem().Id.ToString());
        entry.PutInt("count", stack.GetCount());
        var patch = stack.GetComponents().AsPatch();
        if (!patch.IsEmpty)
        {
            var encoded = DataComponentPatch.PersistentCodec.EncodeStart(NbtOps.Instance, patch);
            if (encoded.Result().IsPresent) entry.Put("components", encoded.GetOrThrow());
        }
        return entry;
    }

    //ReadNbt reads a stack back, returns an empty stack when the id is missing, the item is unregistered or the count is invalid
    //The registry has a default value, lookup must go through ResourceKey, otherwise unknown items silently hit the default
    public static ItemStack ReadNbt(CompoundTag? tag)
    {
        if (tag is null) return Empty;
        var idText = tag.GetStringValue("id");
        var count = tag.GetIntValue("count");
        if (idText.Length == 0 || count <= 0) return Empty;
        var itemId = Identifier.TryParse(idText);
        if (itemId is null) return Empty;
        var holder = BuiltInRegistries.ITEM.GetValue(ResourceKey<Item>.Create(Registries.ITEM, itemId.Value));
        if (holder is null) return Empty;
        var patch = DataComponentPatch.Empty;
        if (tag.GetCompound("components") is { } components)
        {
            var parsed = DataComponentPatch.PersistentCodec.Parse(NbtOps.Instance, components);
            if (parsed.Result().IsPresent) patch = parsed.GetOrThrow();
        }
        return new ItemStack(holder.BuiltInRegistryHolder, count, patch);
    }
}

//ItemStackOptionalStreamCodec allows empty stacks, maps to vanilla OPTIONAL_STREAM_CODEC
//count<=0 is treated as EMPTY, an empty stack encodes count=0
internal sealed class ItemStackOptionalStreamCodec : StreamCodec<RegistryFriendlyByteBuf, ItemStack>
{
    public ItemStack Decode(RegistryFriendlyByteBuf buf)
    {
        int count = buf.ReadVarInt();
        if (count <= 0)
            return ItemStack.Empty;
        var item = ItemCodecs.StreamCodec.Decode(buf);
        var patch = DataComponentPatch.StreamCodec.Decode(buf);
        return new ItemStack(item, count, patch);
    }

    public void Encode(RegistryFriendlyByteBuf buf, ItemStack value)
    {
        if (value.IsEmpty())
        {
            buf.WriteVarInt(0);
            return;
        }
        buf.WriteVarInt(value.GetCount());
        ItemCodecs.StreamCodec.Encode(buf, value.GetTypeHolder()!);
        DataComponentPatch.StreamCodec.Encode(buf, value.GetComponents().AsPatch());
    }
}

//ItemStackStreamCodec forbids empty stacks, maps to vanilla STREAM_CODEC
//Empty stacks throw on encode/decode
internal sealed class ItemStackStreamCodec : StreamCodec<RegistryFriendlyByteBuf, ItemStack>
{
    public ItemStack Decode(RegistryFriendlyByteBuf buf)
    {
        var stack = ItemStack.OptionalStreamCodec.Decode(buf);
        if (stack.IsEmpty())
            throw new InvalidOperationException("Empty ItemStack not allowed");
        return stack;
    }

    public void Encode(RegistryFriendlyByteBuf buf, ItemStack value)
    {
        if (value.IsEmpty())
            throw new InvalidOperationException("Empty ItemStack not allowed");
        ItemStack.OptionalStreamCodec.Encode(buf, value);
    }
}
