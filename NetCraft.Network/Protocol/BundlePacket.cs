namespace NetCraft.Network.Protocol;

//BundlePacket 包打包基类对应原版 net.minecraft.network.protocol.BundlePacket
//把多个小包合成一个 bundle 包传输降低帧开销
//子类提供 Type 并构造时传入子包迭代器
public abstract class BundlePacket<THandler> : Packet<THandler>
{
    private readonly IEnumerable<Packet<THandler>> _packets;

    protected BundlePacket(IEnumerable<Packet<THandler>> packets)
    {
        _packets = packets;
    }

    //Type 子类提供具体包类型标识
    public abstract PacketType<THandler> Type { get; }

    //Handle 子类实现具体处理逻辑
    public abstract void Handle(THandler handler);

    //SubPackets 返回子包迭代器
    public IEnumerable<Packet<THandler>> SubPackets() => _packets;
}
