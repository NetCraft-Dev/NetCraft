namespace NetCraft.Network;

//PacketListener packet listener interface, maps to vanilla net.minecraft.network.PacketListener
//All protocol handlers implement this interface to receive their protocol's packets
//THandler is a self-referential constraint so subclasses must be themselves, preventing misuse
public interface PacketListener
{
    //Flow is the packet direction, SERVERBOUND or CLIENTBOUND
    FlowDirection Flow { get; }

    //Protocol is the current protocol state
    ConnectionProtocol Protocol { get; }

    //OnDisconnect is called when the connection disconnects
    void OnDisconnect(string reason);

    //IsAcceptingMessages indicates whether new packets are accepted, true by default
    bool IsAcceptingMessages => true;

    //ShouldHandleMessage indicates whether to handle the packet, defaults to IsAcceptingMessages
    bool ShouldHandleMessage<THandler>(Packet<THandler> packet) where THandler : class
        => IsAcceptingMessages;
}

//ServerboundPacketListener server-side inbound listener, maps to vanilla net.minecraft.network.ServerboundPacketListener
//Flow is fixed to SERVERBOUND
public interface ServerboundPacketListener : PacketListener
{
    FlowDirection PacketListener.Flow => FlowDirection.Serverbound;
}

//ClientboundPacketListener client-side inbound listener, maps to vanilla net.minecraft.network.ClientboundPacketListener
//Flow is fixed to CLIENTBOUND
public interface ClientboundPacketListener : PacketListener
{
    FlowDirection PacketListener.Flow => FlowDirection.Clientbound;
}

//DisconnectionDetails disconnection details, maps to vanilla net.minecraft.network.DisconnectionDetails
//The simplified form only contains the reason string
public sealed record DisconnectionDetails(string Reason);

//TickablePacketListener tickable listener, maps to vanilla net.minecraft.network.TickablePacketListener
//Connection.Tick calls it once per tick so the listener can poll asynchronous tasks
public interface TickablePacketListener : PacketListener
{
    //TickListener is called every tick, maps to vanilla tick()
    void TickListener();
}
