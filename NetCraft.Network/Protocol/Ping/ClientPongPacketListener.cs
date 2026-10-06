namespace NetCraft.Network.Protocol.Ping;

//ClientPongPacketListener client pong listener, maps to vanilla net.minecraft.network.protocol.ping.ClientPongPacketListener
//Inherits the basic PacketListener interface to handle ClientboundPongResponsePacket
public interface ClientPongPacketListener : PacketListener
{
    //HandlePongResponse handles the pong response packet
    void HandlePongResponse(ClientboundPongResponsePacket packet);
}
