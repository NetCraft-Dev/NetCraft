using System.Collections.Concurrent;

namespace NetCraft.Network;

//PacketTypeRegistry 协议包类型注册表对应原版 ConnectionProtocol 的 packets 映射
//按 (Protocol, Direction, Id) 索引 PacketType 用于反序列化时查找类型
public static class PacketTypeRegistry
{
    private static readonly ConcurrentDictionary<(ConnectionProtocol, FlowDirection, int), IPacketType> _byId = new();

    //Register 注册一个包类型
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

    //FindById 按 (Protocol, Direction, Id) 查找类型
    public static IPacketType? FindById(ConnectionProtocol protocol, FlowDirection direction, int id)
    {
        return _byId.TryGetValue((protocol, direction, id), out var type) ? type : null;
    }

    //Clear 清空注册表测试用
    public static void Clear()
    {
        _byId.Clear();
    }
}
