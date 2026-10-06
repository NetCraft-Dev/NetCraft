namespace NetCraft.Network.Protocol;

//BundlePacket packet bundling base class, maps to vanilla net.minecraft.network.protocol.BundlePacket
//Combines several small packets into one bundle packet for transport, reducing frame overhead
//Subclasses provide Type and pass a subpacket iterator at construction
public abstract class BundlePacket<THandler> : Packet<THandler>
{
    private readonly IEnumerable<Packet<THandler>> _packets;

    protected BundlePacket(IEnumerable<Packet<THandler>> packets)
    {
        _packets = packets;
    }

    //Type subclasses provide the concrete packet type identity
    public abstract PacketType<THandler> Type { get; }

    //Handle subclasses implement the concrete handling logic
    public abstract void Handle(THandler handler);

    //SubPackets returns the subpacket iterator
    public IEnumerable<Packet<THandler>> SubPackets() => _packets;
}
