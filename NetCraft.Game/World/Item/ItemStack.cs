using NetCraft.Nbt;
using NetCraft.Network;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items;

//ItemStack 物品栈对应原版 net.minecraft.world.item.ItemStack
//持有 Holder<Item> count PatchedDataComponentMap 三元组
//STREAM_CODEC 编码 count+Item.STREAM_CODEC+DataComponentPatch.STREAM_CODEC
//OPTIONAL_STREAM_CODEC 允许空栈 count<=0 视为 EMPTY
//STREAM_CODEC 在 OPTIONAL 基础上禁止空栈编解码抛 EncoderException/DecoderException
public sealed class ItemStack : ItemInstance
{
    //Empty 空栈单例 _item=null
    public static readonly ItemStack Empty = new();

    //OptionalStreamCodec 允许空栈编解码
    public static readonly StreamCodec<RegistryFriendlyByteBuf, ItemStack> OptionalStreamCodec
        = new ItemStackOptionalStreamCodec();

    //StreamCodec 禁止空栈编解码
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

    //GetCount 物品数量
    public int GetCount() => _count;

    //Count 数量 对应 ItemInstance 只读视图
    public int Count => _count;

    //TypeHolder 物品类型句柄 对应 ItemInstance 只读视图 空栈抛异常
    public Holder<Item> TypeHolder => _item ?? throw new InvalidOperationException("Cannot get item from empty ItemStack");

    //Get 查该栈的组件 对应 ItemInstance 只读视图
    public T? Get<T>(DataComponentType<T> type) where T : class => _components.Get(type);

    //GetOrDefault 查组件缺失时用兜底值 对应原版 getOrDefault
    public T GetOrDefault<T>(DataComponentType<T> type, T fallback) where T : class => Get(type) ?? fallback;

    //Set 覆写组件 对应原版 set 空栈不允许改
    public void Set<T>(DataComponentType<T> type, T value) where T : class
    {
        if (IsEmpty()) throw new InvalidOperationException("Cannot modify an empty ItemStack");
        _components.Set(type, value);
    }

    //Remove 移除组件 对应原版 remove
    public void Remove<T>(DataComponentType<T> type) where T : class
    {
        if (IsEmpty()) return;
        _components.Remove(type);
    }

    //SetCount 设置数量
    public void SetCount(int count)
    {
        _count = count;
    }

    //Shrink 数量减少 落到 0 为止 对应原版 shrink
    public void Shrink(int amount) => _count = Math.Max(0, _count - amount);

    //Split 取出 amount 个做新栈 原栈相应减少 对应原版 split
    public ItemStack Split(int amount)
    {
        var taken = Math.Min(amount, _count);
        if (taken <= 0) return Empty;
        var result = CopyWithCount(taken);
        Shrink(taken);
        return result;
    }

    //GetMaxStackSize 堆叠上限 空栈按 64 对应原版 getMaxStackSize
    public int GetMaxStackSize() => IsEmpty() ? 64 : GetItem().GetDefaultMaxStackSize();

    //IsStackable 该栈能否继续参与堆叠 对应原版 isStackable
    //原版还要求未损耗耐久 耐久组件接入前只按堆叠上限判定
    public bool IsStackable() => GetMaxStackSize() > 1;

    //IsSameItemAndComponentsAs 同物品且组件相同 对应原版 isSameItemSameComponents
    //逐键比较组件补丁 只比补丁引用的话组件系统接入后会把不同附魔当成同种
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

    //IsEmpty 是否空栈
    public bool IsEmpty() => _item is null || _count <= 0;

    //GetItem 获取 Item 空栈抛异常
    public Item GetItem()
    {
        if (_item is null)
            throw new InvalidOperationException("Cannot get item from empty ItemStack");
        return _item.Value;
    }

    //GetTypeHolder 获取 Holder<Item> 空栈返回 null
    public Holder<Item>? GetTypeHolder() => _item;

    //GetComponents 获取组件映射
    public PatchedDataComponentMap GetComponents() => _components;

    //Copy 复制物品栈
    public ItemStack Copy()
    {
        if (IsEmpty()) return Empty;
        return new ItemStack(_item!, _count, _components.AsPatch());
    }

    //CopyWithCount 按指定数量复制
    public ItemStack CopyWithCount(int count)
    {
        if (IsEmpty()) return Empty;
        return new ItemStack(_item!, count, _components.AsPatch());
    }

    //WriteNbt 按原版 1.20.5+ 的 id + count 结构写出物品栈 空栈不写
    //组件层未接入 写出时丢掉 与 PlayerDataStorage 的存档口径一致
    public static void WriteNbt(CompoundTag parent, string key, ItemStack stack)
    {
        if (stack.IsEmpty()) return;
        var entry = new CompoundTag();
        entry.PutString("id", stack.GetItem().Id.ToString());
        entry.PutInt("count", stack.GetCount());
        parent.Put(key, entry);
    }

    //ReadNbt 读回物品栈 缺 id 或物品未注册或数量非法一律返回空栈
    //注册表带默认值 必须按 ResourceKey 查 否则未知物品会静默落到默认项
    public static ItemStack ReadNbt(CompoundTag? tag)
    {
        if (tag is null) return Empty;
        var idText = tag.GetStringValue("id");
        var count = tag.GetIntValue("count");
        if (idText.Length == 0 || count <= 0) return Empty;
        var itemId = Identifier.TryParse(idText);
        if (itemId is null) return Empty;
        var holder = BuiltInRegistries.ITEM.GetValue(ResourceKey<Item>.Create(Registries.ITEM, itemId.Value));
        return holder is null ? Empty : new ItemStack(holder.BuiltInRegistryHolder, count, DataComponentPatch.Empty);
    }
}

//ItemStackOptionalStreamCodec 允许空栈编解码对应原版 OPTIONAL_STREAM_CODEC
//count<=0 视为 EMPTY 编码空栈写 count=0
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

//ItemStackStreamCodec 禁止空栈编解码对应原版 STREAM_CODEC
//空栈 encode/decode 抛异常
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
