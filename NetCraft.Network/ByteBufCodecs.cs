using NetCraft.Registry;

namespace NetCraft.Network;

//ByteBufCodecs 流编解码工具集对应原版 net.minecraft.network.codec.ByteBufCodecs
//提供注册表 Holder 编解码集合编解码等基础 StreamCodec 工厂
public static class ByteBufCodecs
{
    //初始集合容量上限避免恶意大长度前缀预分配爆内存
    public const int MaxInitialCollectionSize = 65536;

    //Holder 编解码从 RegistryFriendlyByteBuf 读 id 转 Holder.Reference
    //registryKey 标识目标注册表 encode 时按值查 id decode 时按 id 取 Reference
    public static StreamCodec<RegistryFriendlyByteBuf, Holder<T>> Holder<T>(ResourceKey<Registry<T>> registryKey) where T : class
        => new HolderStreamCodec<T>(registryKey);

    //Holder 编解码带 directCodec 支持 Direct 包装值 id==0 走 directCodec 否则 id-1 走注册表 Reference
    //对应原版 ByteBufCodecs.holder(registryKey, directCodec)
    public static StreamCodec<RegistryFriendlyByteBuf, Holder<T>> Holder<T>(
        ResourceKey<Registry<T>> registryKey,
        StreamCodec<RegistryFriendlyByteBuf, T> directCodec) where T : class
        => new DirectHolderStreamCodec<T>(registryKey, directCodec);

    //Collection 编解码 VarInt 长度前缀 + 元素列表
    //elementCodec 单元素编解码 maxSize 长度上限校验
    public static StreamCodec<B, List<V>> Collection<B, V>(StreamCodec<B, V> elementCodec, int maxSize = int.MaxValue) where B : class
        => new CollectionStreamCodec<B, V>(elementCodec, maxSize);
}

//HolderStreamCodec 注册表 id 与 Holder.Reference 双向编解码
internal sealed class HolderStreamCodec<T> : StreamCodec<RegistryFriendlyByteBuf, Holder<T>> where T : class
{
    private readonly ResourceKey<Registry<T>> _registryKey;

    public HolderStreamCodec(ResourceKey<Registry<T>> registryKey)
    {
        _registryKey = registryKey;
    }

    public Holder<T> Decode(RegistryFriendlyByteBuf buf)
    {
        int id = buf.ReadVarInt();
        var registry = buf.Lookup(_registryKey);
        var holder = registry.Get(id);
        if (holder is null)
            throw new InvalidOperationException($"未知 holder id {id} in {_registryKey}");
        return holder;
    }

    public void Encode(RegistryFriendlyByteBuf buf, Holder<T> value)
    {
        if (value is not Reference<T> reference)
            throw new InvalidOperationException($"无法编码非 Reference holder: {value}");
        var registry = buf.Lookup(_registryKey);
        int id = registry.GetId(reference.Value);
        if (id == IdMap<T>.Default)
            throw new InvalidOperationException($"holder 值未注册: {reference.Value}");
        buf.WriteVarInt(id);
    }
}

//DirectHolderStreamCodec 注册表 id + Direct 包装值混合编解码对应原版 ByteBufCodecs.holder(registryKey, directCodec)
//DIRECT_HOLDER_ID = 0 留给 Direct Reference 用 id+1 写 decode 时 id==0 走 directCodec 否则 id-1 查注册表
internal sealed class DirectHolderStreamCodec<T> : StreamCodec<RegistryFriendlyByteBuf, Holder<T>> where T : class
{
    private const int DirectHolderId = 0;

    private readonly ResourceKey<Registry<T>> _registryKey;
    private readonly StreamCodec<RegistryFriendlyByteBuf, T> _directCodec;

    public DirectHolderStreamCodec(ResourceKey<Registry<T>> registryKey, StreamCodec<RegistryFriendlyByteBuf, T> directCodec)
    {
        _registryKey = registryKey;
        _directCodec = directCodec;
    }

    public Holder<T> Decode(RegistryFriendlyByteBuf buf)
    {
        int id = buf.ReadVarInt();
        if (id == DirectHolderId)
            return Holder<T>.Direct(_directCodec.Decode(buf));
        var registry = buf.Lookup(_registryKey);
        var holder = registry.Get(id - 1);
        if (holder is null)
            throw new InvalidOperationException($"未知 holder id {id} in {_registryKey}");
        return holder;
    }

    public void Encode(RegistryFriendlyByteBuf buf, Holder<T> value)
    {
        if (value.HolderKind == Holder<T>.Kind.Reference)
        {
            var registry = buf.Lookup(_registryKey);
            int id = registry.GetId(value.Value);
            if (id == IdMap<T>.Default)
                throw new InvalidOperationException($"holder 值未注册: {value.Value}");
            buf.WriteVarInt(id + 1);
        }
        else
        {
            buf.WriteVarInt(DirectHolderId);
            _directCodec.Encode(buf, value.Value);
        }
    }
}

//CollectionStreamCodec 集合编解码 VarInt 长度前缀逐元素编解码
internal sealed class CollectionStreamCodec<B, V> : StreamCodec<B, List<V>> where B : class
{
    private readonly StreamCodec<B, V> _elementCodec;
    private readonly int _maxSize;

    public CollectionStreamCodec(StreamCodec<B, V> elementCodec, int maxSize)
    {
        _elementCodec = elementCodec;
        _maxSize = maxSize;
    }

    public List<V> Decode(B buf)
    {
        int size = ReadVarInt(buf);
        if (size > _maxSize)
            throw new InvalidOperationException($"集合长度超限 {size} > {_maxSize}");
        var list = new List<V>(Math.Min(size, ByteBufCodecs.MaxInitialCollectionSize));
        for (int i = 0; i < size; i++)
            list.Add(_elementCodec.Decode(buf));
        return list;
    }

    public void Encode(B buf, List<V> value)
    {
        if (value.Count > _maxSize)
            throw new InvalidOperationException($"集合长度超限 {value.Count} > {_maxSize}");
        WriteVarInt(buf, value.Count);
        foreach (var item in value)
            _elementCodec.Encode(buf, item);
    }

    //ReadVarInt 从 FriendlyByteBuf 读 VarInt 用反射避免 B 类型绑定 FriendlyByteBuf
    //实际 B 都是 FriendlyByteBuf 子类调用其 ReadVarInt 方法
    private static int ReadVarInt(B buf)
    {
        if (buf is FriendlyByteBuf fbb) return fbb.ReadVarInt();
        throw new InvalidOperationException($"不支持的 buffer 类型 {buf?.GetType()}");
    }

    private static void WriteVarInt(B buf, int value)
    {
        if (buf is FriendlyByteBuf fbb) { fbb.WriteVarInt(value); return; }
        throw new InvalidOperationException($"不支持的 buffer 类型 {buf?.GetType()}");
    }
}
