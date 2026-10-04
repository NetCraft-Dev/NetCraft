namespace NetCraft.Network.Protocol;

//ProtocolInfoBuilder 协议信息构建器对应原版 net.minecraft.network.protocol.ProtocolInfoBuilder
//按协议+方向注册 PacketType → StreamCodec 映射最后构建 SimpleUnboundProtocol
//THandler 是包处理器类型所有注册的包都继承 Packet<THandler>
public sealed class ProtocolInfoBuilder<THandler>
{
    private readonly ConnectionProtocol _protocol;
    private readonly FlowDirection _flow;
    private readonly List<CodecEntry> _codecs = new();
    private BundlerInfo<THandler>? _bundlerInfo;

    public ProtocolInfoBuilder(ConnectionProtocol protocol, FlowDirection flow)
    {
        _protocol = protocol;
        _flow = flow;
    }

    //AddPacket 注册包类型和对应编解码器无 CodecModifier
    //serializer 接受 RegistryFriendlyByteBuf 因 StreamCodec B 逆变 FriendlyByteBuf codec 可隐式传入
    public ProtocolInfoBuilder<THandler> AddPacket<TPacket>(
        PacketType<THandler> type,
        StreamCodec<RegistryFriendlyByteBuf, TPacket> serializer)
        where TPacket : Packet<THandler>
    {
        _codecs.Add(new CodecEntry(type, typeof(TPacket), WrapCodec(serializer)));
        return this;
    }

    //AddPacket 注册包类型和对应编解码器带 CodecModifier
    public ProtocolInfoBuilder<THandler> AddPacket<TPacket>(
        PacketType<THandler> type,
        StreamCodec<RegistryFriendlyByteBuf, TPacket> serializer,
        CodecModifier<RegistryFriendlyByteBuf, TPacket, object> modifier)
        where TPacket : Packet<THandler>
    {
        //简化版 CodecModifier 暂不应用直接用原始 serializer
        _codecs.Add(new CodecEntry(type, typeof(TPacket), WrapCodec(serializer)));
        return this;
    }

    //AddPacketCommon 把父监听器(common/cookie)的包桥接到当前子监听器(THandler)协议注册
    //C# Packet<T> 不变体不能直接满足 Packet<THandler> 约束故用 BridgeSuperCodec 包装
    //decode 产物是 BridgedSuperPacket 使静态类型为 Packet<THandler> 派发由 PacketProcessor 反射定位调用
    public ProtocolInfoBuilder<THandler> AddPacketCommon<TSuper, TPacket>(
        PacketType<TSuper> type,
        StreamCodec<RegistryFriendlyByteBuf, TPacket> serializer)
        where TSuper : class
        where TPacket : class, Packet<TSuper>
    {
        var handlerType = new PacketType<THandler>(type.Id, type.Protocol, type.Direction, null);
        _codecs.Add(new CodecEntry(handlerType, typeof(TPacket), new BridgeSuperCodec<TSuper, TPacket, THandler>(serializer, handlerType)));
        return this;
    }

    //WithBundlePacket 注册 bundle 包类型分隔符包和打包信息
    public ProtocolInfoBuilder<THandler> WithBundlePacket<TBundle>(
        PacketType<THandler> bundlerPacketType,
        Func<IEnumerable<Packet<THandler>>, TBundle> constructor,
        BundleDelimiterPacket<THandler> delimiterPacket)
        where TBundle : BundlePacket<THandler>
    {
        _codecs.Add(new CodecEntry(delimiterPacket.Type, delimiterPacket.GetType(), WrapCodec(CreateUnitCodec(delimiterPacket))));
        _bundlerInfo = BundlerInfo<THandler>.CreateForPacket(
            bundlerPacketType,
            packets => constructor(packets),
            delimiterPacket);
        return this;
    }

    //BuildUnbound 构建未绑定协议返回 SimpleUnboundProtocol
    public SimpleUnboundProtocol<THandler> BuildUnbound()
    {
        var listCopy = _codecs.ToList();
        var bundlerInfo = _bundlerInfo;
        var details = new DetailsImpl<THandler>(_protocol, _flow, listCopy);
        return new SimpleUnboundProtocolImpl<THandler>(_protocol, _flow, listCopy, bundlerInfo, details);
    }

    //CreateUnitCodec 创建恒定值编解码器对应原版 StreamCodec.unit
    private static StreamCodec<RegistryFriendlyByteBuf, TPacket> CreateUnitCodec<TPacket>(TPacket instance)
        where TPacket : Packet<THandler>
        => new UnitStreamCodec<RegistryFriendlyByteBuf, TPacket>(instance);

    //WrapCodec 把子包编解码器包装为 Packet<THandler> 编解码器
    private static StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>> WrapCodec<TPacket>(StreamCodec<RegistryFriendlyByteBuf, TPacket> serializer)
        where TPacket : Packet<THandler>
        => new WrappedCodec<TPacket, THandler>(serializer);

    public readonly struct CodecEntry
    {
        public PacketType<THandler> Type { get; }

        //PacketClass 该条注册的包 C# 类型编码时据此在当前协议表反查网络 ID
        public Type PacketClass { get; }

        public StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>> Serializer { get; }
        public CodecEntry(PacketType<THandler> type, Type packetClass, StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>> serializer)
        {
            Type = type;
            PacketClass = packetClass;
            Serializer = serializer;
        }
    }
}

//DetailsImpl ProtocolInfo.Details 实现
file sealed class DetailsImpl<THandler> : ProtocolInfo<THandler>.Details
{
    private readonly ConnectionProtocol _protocol;
    private readonly FlowDirection _flow;
    private readonly List<ProtocolInfoBuilder<THandler>.CodecEntry> _codecs;

    public DetailsImpl(
        ConnectionProtocol protocol,
        FlowDirection flow,
        List<ProtocolInfoBuilder<THandler>.CodecEntry> codecs)
    {
        _protocol = protocol;
        _flow = flow;
        _codecs = codecs;
    }

    public ConnectionProtocol Id => _protocol;
    public Protocol.PacketFlow Flow => _flow.ToPacketFlow();

    public void ListPackets(ProtocolInfo<THandler>.Details.PacketVisitor output)
    {
        for (int i = 0; i < _codecs.Count; i++)
        {
            var entry = _codecs[i];
            output.Accept(entry.Type, entry.Type.Id);
        }
    }
}

//SimpleUnboundProtocolImpl SimpleUnboundProtocol 实现
file sealed class SimpleUnboundProtocolImpl<THandler> : SimpleUnboundProtocol<THandler>
{
    private readonly ConnectionProtocol _protocol;
    private readonly FlowDirection _flow;
    private readonly List<ProtocolInfoBuilder<THandler>.CodecEntry> _codecs;
    private readonly BundlerInfo<THandler>? _bundlerInfo;
    private readonly ProtocolInfo<THandler>.Details _details;

    public SimpleUnboundProtocolImpl(
        ConnectionProtocol protocol,
        FlowDirection flow,
        List<ProtocolInfoBuilder<THandler>.CodecEntry> codecs,
        BundlerInfo<THandler>? bundlerInfo,
        ProtocolInfo<THandler>.Details details)
    {
        _protocol = protocol;
        _flow = flow;
        _codecs = codecs;
        _bundlerInfo = bundlerInfo;
        _details = details;
    }

    public ProtocolInfo<THandler>.Details Details => _details;

    //Bind 构建 ProtocolInfo 直接构造 IdDispatchStreamCodec
    //byClass 是「包类 → 本协议网络 ID」同一包类可在多个协议注册成不同 id
    //编码时按它取 id 而不是包自带 Type.Id 对齐原版由协议表分配 id 的行为
    public ProtocolInfo<THandler> Bind()
    {
        var byId = new Dictionary<int, (PacketType<THandler>, StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>>)>();
        var byClass = new Dictionary<Type, int>();
        foreach (var entry in _codecs)
        {
            byId[entry.Type.Id] = (entry.Type, entry.Serializer);
            byClass[entry.PacketClass] = entry.Type.Id;
        }
        var codec = new IdDispatchStreamCodec<THandler>(byId, byClass);
        return new ProtocolInfoImpl<THandler>(_protocol, _flow, codec, _bundlerInfo);
    }
}

//ProtocolInfoImpl ProtocolInfo 实现
file sealed class ProtocolInfoImpl<THandler> : ProtocolInfo<THandler>
{
    private readonly ConnectionProtocol _protocol;
    private readonly FlowDirection _flow;
    private readonly StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>> _codec;
    private readonly BundlerInfo<THandler>? _bundlerInfo;

    public ProtocolInfoImpl(
        ConnectionProtocol protocol,
        FlowDirection flow,
        StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>> codec,
        BundlerInfo<THandler>? bundlerInfo)
    {
        _protocol = protocol;
        _flow = flow;
        _codec = codec;
        _bundlerInfo = bundlerInfo;
    }

    public ConnectionProtocol Id => _protocol;
    public Protocol.PacketFlow Flow => _flow.ToPacketFlow();
    public StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>> Codec => _codec;
    public BundlerInfo<THandler> BundlerInfo => _bundlerInfo ?? EmptyBundlerInfo<THandler>.Instance;
}

//EmptyBundlerInfo 无 bundle 时使用的空 BundlerInfo
file sealed class EmptyBundlerInfo<THandler> : BundlerInfo<THandler>
{
    public static EmptyBundlerInfo<THandler> Instance { get; } = new();

    public void UnbundlePacket(Packet<THandler> packet, Action<Packet<THandler>> output)
        => output(packet);

    public BundlerInfo<THandler>.Bundler<THandler>? StartPacketBundling(Packet<THandler> packet)
        => null;
}

//BridgeSuperCodec 把父监听器(common/cookie)子包编解码器包装为当前子监听器(THandler)的 Packet 编解码器
//decode 产物是 BridgedSuperPacket 运行时派发由 PacketProcessor 反射回退定位 common 包的 Handle 方法
internal sealed class BridgeSuperCodec<TSuper, TPacket, THandler> : StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>>, IObjectEncodable
    where TSuper : class
    where TPacket : class, Packet<TSuper>
{
    private readonly StreamCodec<RegistryFriendlyByteBuf, TPacket> _inner;
    private readonly PacketType<THandler> _type;

    public BridgeSuperCodec(StreamCodec<RegistryFriendlyByteBuf, TPacket> inner, PacketType<THandler> type)
    {
        _inner = inner;
        _type = type;
    }

    public Packet<THandler> Decode(RegistryFriendlyByteBuf buf)
        => new BridgedSuperPacket<TSuper, TPacket, THandler>(_inner.Decode(buf), _type);

    public void Encode(RegistryFriendlyByteBuf buf, Packet<THandler> value)
        => EncodeCore(buf, value);

    //EncodeObject 非泛型编码路径 服务端直接发原始 common 包时 value 实际类型即 TPacket
    void IObjectEncodable.EncodeObject(RegistryFriendlyByteBuf buf, object value)
        => EncodeCore(buf, value);

    //EncodeCore 支持桥接包取 Inner 和直接发原始 common 包两种形式
    private void EncodeCore(RegistryFriendlyByteBuf buf, object value)
    {
        var inner = value as BridgedSuperPacket<TSuper, TPacket, THandler> is { } bridge ? bridge.Inner : value as TPacket;
        if (inner is null)
            throw new ArgumentException($"non-bridged packet 不能经 {nameof(BridgeSuperCodec<TSuper, TPacket, THandler>)} 编码");
        _inner.Encode(buf, inner);
    }
}

//BridgedSuperPacket 父监听器包的适配实现使静态类型满足 Packet<THandler>
//Type 用传入的桥接 PacketType 供 IdDispatch Encode 取 id 派发网络 ID
internal sealed class BridgedSuperPacket<TSuper, TPacket, THandler> : Packet<THandler>
    where TSuper : class
    where TPacket : Packet<TSuper>
{
    private readonly PacketType<THandler> _type;

    public BridgedSuperPacket(TPacket inner, PacketType<THandler> type)
    {
        Inner = inner;
        _type = type;
    }

    //Inner 内部父监听器原始包
    public TPacket Inner { get; }

    public PacketType<THandler> Type => _type;

    //IsSkippable 转发内层包 桥接包装要对调用方完全透明 否则默认值会盖掉内层语义
    public bool IsSkippable => Inner.IsSkippable;

    //IsTerminal 转发内层包 断开包靠它触发连接关闭 不转发的话桥接后连接不会断
    public bool IsTerminal => Inner.IsTerminal;

    public void Handle(THandler handler)
        => Inner.Handle((TSuper)(object)handler!);
}
