namespace NetCraft.Network.Protocol;

//BundleDelimiterPacket packet delimiter base class, maps to vanilla net.minecraft.network.protocol.BundleDelimiterPacket
//Marks the start and end boundaries of a bundle; the packet is handled by the pipeline and should not reach Handle
public abstract class BundleDelimiterPacket<THandler> : Packet<THandler>
{
    //Type subclasses provide the concrete packet type identity
    public abstract PacketType<THandler> Type { get; }

    //Handle throws since a delimiter packet should not be received by the handler
    public void Handle(THandler handler)
        => throw new InvalidOperationException("a delimiter packet should be handled by the pipeline and should not reach Handle");
}
