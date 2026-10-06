using System.Collections.Concurrent;

namespace NetCraft.Network;

//PacketTypeRegistry protocol packet type registry, maps to the packets map of vanilla ConnectionProtocol
//Indexes PacketType by (Protocol, Direction, Id) to look up the type during deserialization
public static class PacketTypeRegistry
{
    private static readonly ConcurrentDictionary<(ConnectionProtocol, FlowDirection, int), IPacketType> _byId = new();

    //Register registers a packet type
    public static PacketType<THandler> Register<THandler>(
        int id,
        ConnectionProtocol protocol,
        FlowDirection direction,
        StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>>? codec = null)
        where THandler : class
    {
        var type = new PacketType<THandler>(id, protocol, direction, codec);
        _byId[(protocol, direction, id)] = type;
        return type;
    }

    //FindById looks up the type by (Protocol, Direction, Id)
    public static IPacketType? FindById(ConnectionProtocol protocol, FlowDirection direction, int id)
    {
        return _byId.TryGetValue((protocol, direction, id), out var type) ? type : null;
    }

    //Clear empties the registry, for tests
    public static void Clear()
    {
        _byId.Clear();
    }
}
