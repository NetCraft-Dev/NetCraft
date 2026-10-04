namespace NetCraft.Network.Protocol;

//ProtocolCodecBuilder 协议编解码器构建器对应原版 net.minecraft.network.protocol.ProtocolCodecBuilder
//按方向注册 PacketType → StreamCodec 映射最后构建 IdDispatchCodec
//THandler 是包处理器类型所有注册的包都继承 Packet<THandler>
public sealed class ProtocolCodecBuilder<THandler>
{
    private readonly FlowDirection _flow;
    private readonly List<Entry> _entries = new();

    public ProtocolCodecBuilder(FlowDirection flow)
    {
        _flow = flow;
    }

    //Add 注册包类型和对应编解码器
    //type.Direction 必须与构建器 Flow 一致否则抛异常
    //TPacket 必须继承 Packet<THandler>
    //serializer 接受 RegistryFriendlyByteBuf 因 StreamCodec B 逆变 FriendlyByteBuf codec 可隐式传入
    public ProtocolCodecBuilder<THandler> Add<TPacket>(
        PacketType<THandler> type,
        StreamCodec<RegistryFriendlyByteBuf, TPacket> serializer)
        where TPacket : Packet<THandler>
    {
        if (type.Direction != _flow)
            throw new ArgumentException($"包 {type} 方向不匹配，期望 {_flow}");
        _entries.Add(new Entry(type, typeof(TPacket), WrapCodec(serializer)));
        return this;
    }

    //Build 构建 IdDispatchStreamCodec 包网络 ID 按 type.Id 分配
    //返回按 VarInt 网络 ID 分发的编解码器
    public StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>> Build()
    {
        var byId = new Dictionary<int, (PacketType<THandler> Type, StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>> Serializer)>();
        var byClass = new Dictionary<Type, int>();
        foreach (var entry in _entries)
        {
            byId[entry.Type.Id] = (entry.Type, entry.Serializer);
            byClass[entry.PacketClass] = entry.Type.Id;
        }
        return new IdDispatchStreamCodec<THandler>(byId, byClass);
    }

    //WrapCodec 把子包编解码器包装为 Packet<THandler> 编解码器
    //解决 C# 泛型不协变问题 TPacket → Packet<THandler>
    private static StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>> WrapCodec<TPacket>(StreamCodec<RegistryFriendlyByteBuf, TPacket> serializer)
        where TPacket : Packet<THandler>
        => new WrappedCodec<TPacket, THandler>(serializer);

    private readonly struct Entry
    {
        public PacketType<THandler> Type { get; }

        //PacketClass 该条注册的包 C# 类型编码时据此在当前协议表反查网络 ID
        public Type PacketClass { get; }

        public StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>> Serializer { get; }
        public Entry(PacketType<THandler> type, Type packetClass, StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>> serializer)
        {
            Type = type;
            PacketClass = packetClass;
            Serializer = serializer;
        }
    }
}

//WrappedCodec 把 StreamCodec<RegistryFriendlyByteBuf, TPacket> 包装为 StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>>
//TPacket : Packet<THandler> 强制 cast 安全
//internal 跨 Protocol 子命名空间文件可见
internal sealed class WrappedCodec<TPacket, THandler> : StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>>, IObjectEncodable
    where TPacket : Packet<THandler>
{
    private readonly StreamCodec<RegistryFriendlyByteBuf, TPacket> _inner;

    public WrappedCodec(StreamCodec<RegistryFriendlyByteBuf, TPacket> inner) => _inner = inner;

    public Packet<THandler> Decode(RegistryFriendlyByteBuf buf) => _inner.Decode(buf);

    public void Encode(RegistryFriendlyByteBuf buf, Packet<THandler> value)
        => _inner.Encode(buf, (TPacket)value);

    //EncodeObject 非泛型编码路径 value 实际类型即 TPacket 强转安全
    void IObjectEncodable.EncodeObject(RegistryFriendlyByteBuf buf, object value)
        => _inner.Encode(buf, (TPacket)value);
}

//IdDispatchStreamCodec 按 VarInt 网络 ID 分发的编解码器
//对应原版 net.minecraft.network.codec.IdDispatchCodec
//解码读 VarInt ID 查表调用子编解码器解码
//编码按「包类 → 本协议 ID」取 ID 对应原版由协议表按注册分配 ID 的行为
public sealed class IdDispatchStreamCodec<THandler> : StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>>, IObjectEncodable
{
    private readonly Dictionary<int, (PacketType<THandler> Type, StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>> Serializer)> _byId;
    private readonly Dictionary<Type, int> _byClass;

    public IdDispatchStreamCodec(
        Dictionary<int, (PacketType<THandler>, StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>>)> byId,
        Dictionary<Type, int> byClass)
    {
        _byId = byId;
        _byClass = byClass;
    }

    public Packet<THandler> Decode(RegistryFriendlyByteBuf buf)
    {
        int id = buf.ReadVarInt();
        if (!_byId.TryGetValue(id, out var entry))
            throw new IOException($"未知包网络 ID {id}");
        return entry.Serializer.Decode(buf);
    }

    public void Encode(RegistryFriendlyByteBuf buf, Packet<THandler> value)
    {
        var id = ResolveId(value);
        buf.WriteVarInt(id);
        if (!_byId.TryGetValue(id, out var entry))
            throw new IOException($"包类型 {value.GetType().Name} 未在当前协议注册");
        entry.Serializer.Encode(buf, value);
    }

    //EncodeObject 非泛型编码路径 common 包静态类型不匹配 Packet<THandler> 时由此进入子 codec 的 object 编码
    void IObjectEncodable.EncodeObject(RegistryFriendlyByteBuf buf, object value)
    {
        var id = ResolveId(value);
        buf.WriteVarInt(id);
        if (!_byId.TryGetValue(id, out var entry))
            throw new IOException($"包类型 {value.GetType().Name} 未在当前协议注册");
        if (entry.Serializer is IObjectEncodable encodable)
            encodable.EncodeObject(buf, value);
        else
            entry.Serializer.Encode(buf, (Packet<THandler>)value);
    }

    //ResolveId 取包在当前协议表的网络 ID
    //同一包类可跨协议注册成不同 ID(update_tags 在 Play 是 134 在 Configuration 是 13)
    //故以协议表登记的「包类 → ID」为准 包自带 Type 只作兜底
    private int ResolveId(object value)
    {
        if (_byClass.TryGetValue(value.GetType(), out var id))
            return id;
        if (value is IPacket packet)
            return packet.PacketTypeId;
        throw new IOException($"包 {value.GetType().Name} 未实现 IPacket");
    }
}
