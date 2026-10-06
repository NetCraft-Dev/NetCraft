namespace NetCraft.Network.Protocol.Ping;

//ServerPingPacketListener server-side ping listener, maps to vanilla net.minecraft.network.protocol.ping.ServerPingPacketListener
//Inherits the basic PacketListener interface to handle ServerboundPingRequestPacket
public interface ServerPingPacketListener : PacketListener
{
    //HandlePingRequest handles the ping request packet
    void HandlePingRequest(ServerboundPingRequestPacket packet);
}
