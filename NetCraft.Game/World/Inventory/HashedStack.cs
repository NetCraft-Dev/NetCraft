using NetCraft.Network;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Inventory;

//HashedStack 哈希栈对应原版 net.minecraft.network.HashedStack
//客户端点击包上报的栈摘要 服务端据此校验客户端预测 本作不校验只解码
//编码对应原版 ByteBufCodecs.optional(ActualItem.STREAM_CODEC) 空栈只写一个 false
public abstract class HashedStack
{
    //Empty 空栈单例
    public static readonly HashedStack Empty = new EmptyStack();

    //StreamCodec 网络编解码
    public static readonly StreamCodec<RegistryFriendlyByteBuf, HashedStack> StreamCodec
        = new HashedStackCodec();

    //ActualItem 非空栈摘要 item holder + 数量 + 组件哈希
    public sealed class ActualItem : HashedStack
    {
        public ActualItem(Holder<Item> item, int count, HashedPatchMap components)
        {
            Item = item;
            Count = count;
            Components = components;
        }

        //Item 物品 holder 对应原版 item
        public Holder<Item> Item { get; }

        //Count 数量 对应原版 count
        public int Count { get; }

        //Components 组件哈希 对应原版 components
        public HashedPatchMap Components { get; }
    }

    private sealed class EmptyStack : HashedStack { }
}

//HashedStackCodec 空栈写 false 非空写 true 加 item holder 加数量加组件哈希
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

//HashedPatchMap 组件补丁哈希对应原版 net.minecraft.network.HashedPatchMap
//addedComponents 记录新增组件类型到值哈希 removedComponents 记录被移除的组件类型
public sealed class HashedPatchMap
{
    //MaxComponents 单项数量上限对应原版 256
    internal const int MaxComponents = 256;

    //StreamCodec 网络编解码
    public static readonly StreamCodec<RegistryFriendlyByteBuf, HashedPatchMap> StreamCodec
        = new HashedPatchMapCodec();

    public HashedPatchMap(Dictionary<object, int> addedComponents, HashSet<object> removedComponents)
    {
        AddedComponents = addedComponents;
        RemovedComponents = removedComponents;
    }

    //AddedComponents 新增组件类型到值哈希
    public IReadOnlyDictionary<object, int> AddedComponents { get; }

    //RemovedComponents 被移除的组件类型
    public IReadOnlySet<object> RemovedComponents { get; }
}

//HashedPatchMapCodec 先写新增项再写移除项 前缀都是 VarInt 数量
internal sealed class HashedPatchMapCodec : StreamCodec<RegistryFriendlyByteBuf, HashedPatchMap>
{
    public HashedPatchMap Decode(RegistryFriendlyByteBuf buf)
    {
        int addedCount = buf.ReadVarInt();
        if (addedCount > HashedPatchMap.MaxComponents)
            throw new InvalidOperationException($"组件哈希新增项超限: {addedCount}");
        var added = new Dictionary<object, int>(Math.Min(addedCount, ByteBufCodecs.MaxInitialCollectionSize));
        for (int i = 0; i < addedCount; i++)
        {
            var type = DataComponentTypeCodecs.Decode(buf);
            added[type] = buf.ReadVarInt();
        }

        int removedCount = buf.ReadVarInt();
        if (removedCount > HashedPatchMap.MaxComponents)
            throw new InvalidOperationException($"组件哈希移除项超限: {removedCount}");
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
