namespace NetCraft.Network.Protocol;

//ProtocolInfoBuilder protocol info builder, maps to vanilla net.minecraft.network.protocol.ProtocolInfoBuilder
//Registers PacketType → StreamCodec mappings by protocol + direction and finally builds a SimpleUnboundProtocol
//THandler is the packet handler type; all registered packets inherit Packet<THandler>
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

    //AddPacket registers a packet type and its codec without a CodecModifier
    //serializer takes RegistryFriendlyByteBuf since StreamCodec's contravariant B lets a FriendlyByteBuf codec be passed implicitly
    public ProtocolInfoBuilder<THandler> AddPacket<TPacket>(
        PacketType<THandler> type,
        StreamCodec<RegistryFriendlyByteBuf, TPacket> serializer)
        where TPacket : Packet<THandler>
    {
        _codecs.Add(new CodecEntry(type, typeof(TPacket), WrapCodec(serializer)));
        return this;
    }

    //AddPacket registers a packet type and its codec with a CodecModifier
    public ProtocolInfoBuilder<THandler> AddPacket<TPacket>(
        PacketType<THandler> type,
        StreamCodec<RegistryFriendlyByteBuf, TPacket> serializer,
        CodecModifier<RegistryFriendlyByteBuf, TPacket, object> modifier)
        where TPacket : Packet<THandler>
    {
        //The simplified form does not apply CodecModifier for now and uses the raw serializer directly
        _codecs.Add(new CodecEntry(type, typeof(TPacket), WrapCodec(serializer)));
        return this;
    }

    //AddPacketCommon bridges packets of the parent listener (common/cookie) into the current child listener (THandler) protocol registration
    //Since C# Packet<T> is invariant and cannot directly satisfy the Packet<THandler> constraint, BridgeSuperCodec wraps it
    //decode produces a BridgedSuperPacket so the static type is Packet<THandler>, with dispatch located and invoked by PacketProcessor via reflection
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

    //WithBundlePacket registers the bundle packet type, the delimiter packet, and the bundling info
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

    //BuildUnbound builds an unbound protocol and returns a SimpleUnboundProtocol
    public SimpleUnboundProtocol<THandler> BuildUnbound()
    {
        var listCopy = _codecs.ToList();
        var bundlerInfo = _bundlerInfo;
        var details = new DetailsImpl<THandler>(_protocol, _flow, listCopy);
        return new SimpleUnboundProtocolImpl<THandler>(_protocol, _flow, listCopy, bundlerInfo, details);
    }

    //CreateUnitCodec creates a constant-value codec, maps to vanilla StreamCodec.unit
    private static StreamCodec<RegistryFriendlyByteBuf, TPacket> CreateUnitCodec<TPacket>(TPacket instance)
        where TPacket : Packet<THandler>
        => new UnitStreamCodec<RegistryFriendlyByteBuf, TPacket>(instance);

    //WrapCodec wraps a subpacket codec into a Packet<THandler> codec
    private static StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>> WrapCodec<TPacket>(StreamCodec<RegistryFriendlyByteBuf, TPacket> serializer)
        where TPacket : Packet<THandler>
        => new WrappedCodec<TPacket, THandler>(serializer);

    public readonly struct CodecEntry
    {
        public PacketType<THandler> Type { get; }

        //PacketClass the C# type of the registered packet, used on encode to look up the network ID in the current protocol table
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

//DetailsImpl ProtocolInfo.Details implementation
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

//SimpleUnboundProtocolImpl SimpleUnboundProtocol implementation
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

    //Bind builds a ProtocolInfo and constructs an IdDispatchStreamCodec directly
    //byClass is "packet class → this protocol's network ID"; the same packet class can be registered under different ids in multiple protocols
    //On encode the id comes from it rather than the packet's own Type.Id, aligning with vanilla's behavior of assigning ids from the protocol table
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

//ProtocolInfoImpl ProtocolInfo implementation
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

//EmptyBundlerInfo the empty BundlerInfo used when there is no bundle
file sealed class EmptyBundlerInfo<THandler> : BundlerInfo<THandler>
{
    public static EmptyBundlerInfo<THandler> Instance { get; } = new();

    public void UnbundlePacket(Packet<THandler> packet, Action<Packet<THandler>> output)
        => output(packet);

    public BundlerInfo<THandler>.Bundler<THandler>? StartPacketBundling(Packet<THandler> packet)
        => null;
}

//BridgeSuperCodec wraps a parent listener (common/cookie) subpacket codec into a Packet codec for the current child listener (THandler)
//decode produces a BridgedSuperPacket, and runtime dispatch falls back to PacketProcessor reflection to locate the common packet's Handle method
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

    //EncodeObject non-generic encode path; when the server sends a raw common packet directly the actual type of value is TPacket
    void IObjectEncodable.EncodeObject(RegistryFriendlyByteBuf buf, object value)
        => EncodeCore(buf, value);

    //EncodeCore supports both a bridged packet taking Inner and sending a raw common packet directly
    private void EncodeCore(RegistryFriendlyByteBuf buf, object value)
    {
        var inner = value as BridgedSuperPacket<TSuper, TPacket, THandler> is { } bridge ? bridge.Inner : value as TPacket;
        if (inner is null)
            throw new ArgumentException($"a non-bridged packet cannot be encoded via {nameof(BridgeSuperCodec<TSuper, TPacket, THandler>)}");
        _inner.Encode(buf, inner);
    }
}

//BridgedSuperPacket adapter implementation for parent listener packets so the static type satisfies Packet<THandler>
//Type uses the passed bridging PacketType so IdDispatch Encode can take the id and dispatch the network ID
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

    //Inner the inner parent listener raw packet
    public TPacket Inner { get; }

    public PacketType<THandler> Type => _type;

    //IsSkippable forwards to the inner packet; the bridging wrapper must be fully transparent to callers, otherwise the default value would override the inner semantics
    public bool IsSkippable => Inner.IsSkippable;

    //IsTerminal forwards to the inner packet; disconnect packets rely on it to trigger connection close, and without forwarding the connection would not close after bridging
    public bool IsTerminal => Inner.IsTerminal;

    public void Handle(THandler handler)
        => Inner.Handle((TSuper)(object)handler!);
}
