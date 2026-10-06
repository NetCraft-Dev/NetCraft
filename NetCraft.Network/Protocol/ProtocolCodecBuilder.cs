namespace NetCraft.Network.Protocol;

//ProtocolCodecBuilder protocol codec builder, maps to vanilla net.minecraft.network.protocol.ProtocolCodecBuilder
//Registers PacketType → StreamCodec mappings by direction and finally builds an IdDispatchCodec
//THandler is the packet handler type; all registered packets inherit Packet<THandler>
public sealed class ProtocolCodecBuilder<THandler>
{
    private readonly FlowDirection _flow;
    private readonly List<Entry> _entries = new();

    public ProtocolCodecBuilder(FlowDirection flow)
    {
        _flow = flow;
    }

    //Add registers a packet type and its codec
    //type.Direction must match the builder's Flow, otherwise an exception is thrown
    //TPacket must inherit Packet<THandler>
    //serializer takes RegistryFriendlyByteBuf since StreamCodec's contravariant B lets a FriendlyByteBuf codec be passed implicitly
    public ProtocolCodecBuilder<THandler> Add<TPacket>(
        PacketType<THandler> type,
        StreamCodec<RegistryFriendlyByteBuf, TPacket> serializer)
        where TPacket : Packet<THandler>
    {
        if (type.Direction != _flow)
            throw new ArgumentException($"packet {type} direction mismatch, expected {_flow}");
        _entries.Add(new Entry(type, typeof(TPacket), WrapCodec(serializer)));
        return this;
    }

    //Build builds an IdDispatchStreamCodec, assigning packet network IDs by type.Id
    //Returns a codec dispatching by VarInt network ID
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

    //WrapCodec wraps a subpacket codec into a Packet<THandler> codec
    //Solves the C# generic non-covariance problem TPacket → Packet<THandler>
    private static StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>> WrapCodec<TPacket>(StreamCodec<RegistryFriendlyByteBuf, TPacket> serializer)
        where TPacket : Packet<THandler>
        => new WrappedCodec<TPacket, THandler>(serializer);

    private readonly struct Entry
    {
        public PacketType<THandler> Type { get; }

        //PacketClass the C# type of the registered packet, used on encode to look up the network ID in the current protocol table
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

//WrappedCodec wraps StreamCodec<RegistryFriendlyByteBuf, TPacket> into StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>>
//TPacket : Packet<THandler> makes the forced cast safe
//internal, visible across files in the Protocol sub-namespace
internal sealed class WrappedCodec<TPacket, THandler> : StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>>, IObjectEncodable
    where TPacket : Packet<THandler>
{
    private readonly StreamCodec<RegistryFriendlyByteBuf, TPacket> _inner;

    public WrappedCodec(StreamCodec<RegistryFriendlyByteBuf, TPacket> inner) => _inner = inner;

    public Packet<THandler> Decode(RegistryFriendlyByteBuf buf) => _inner.Decode(buf);

    public void Encode(RegistryFriendlyByteBuf buf, Packet<THandler> value)
        => _inner.Encode(buf, (TPacket)value);

    //EncodeObject non-generic encode path; the actual type of value is TPacket so the cast is safe
    void IObjectEncodable.EncodeObject(RegistryFriendlyByteBuf buf, object value)
        => _inner.Encode(buf, (TPacket)value);
}

//IdDispatchStreamCodec codec dispatching by VarInt network ID
//Maps to vanilla net.minecraft.network.codec.IdDispatchCodec
//Decode reads a VarInt ID and calls the sub-codec's decode via a table lookup
//Encode takes the ID from "packet class → this protocol's ID", matching vanilla's behavior of assigning IDs by registration in the protocol table
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
            throw new IOException($"unknown packet network ID {id}");
        return entry.Serializer.Decode(buf);
    }

    public void Encode(RegistryFriendlyByteBuf buf, Packet<THandler> value)
    {
        var id = ResolveId(value);
        buf.WriteVarInt(id);
        if (!_byId.TryGetValue(id, out var entry))
            throw new IOException($"packet type {value.GetType().Name} is not registered in the current protocol");
        entry.Serializer.Encode(buf, value);
    }

    //EncodeObject non-generic encode path; when a common packet's static type does not match Packet<THandler> this enters the sub-codec's object encoding
    void IObjectEncodable.EncodeObject(RegistryFriendlyByteBuf buf, object value)
    {
        var id = ResolveId(value);
        buf.WriteVarInt(id);
        if (!_byId.TryGetValue(id, out var entry))
            throw new IOException($"packet type {value.GetType().Name} is not registered in the current protocol");
        if (entry.Serializer is IObjectEncodable encodable)
            encodable.EncodeObject(buf, value);
        else
            entry.Serializer.Encode(buf, (Packet<THandler>)value);
    }

    //ResolveId gets the packet's network ID from the current protocol table
    //The same packet class can be registered under different IDs across protocols (update_tags is 134 in Play and 13 in Configuration)
    //So the "packet class → ID" recorded in the protocol table wins, with the packet's own Type used only as a fallback
    private int ResolveId(object value)
    {
        if (_byClass.TryGetValue(value.GetType(), out var id))
            return id;
        if (value is IPacket packet)
            return packet.PacketTypeId;
        throw new IOException($"packet {value.GetType().Name} does not implement IPacket");
    }
}
