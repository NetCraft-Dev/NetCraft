using NetCraft.Network.Protocol.Ping;

namespace NetCraft.Network.Protocol.Status;

//ServerStatusPacketListener server-side status listener, maps to vanilla net.minecraft.network.protocol.status.ServerStatusPacketListener
//Inherits ServerboundPacketListener and ServerPingPacketListener
//Protocol is fixed to STATUS
public interface ServerStatusPacketListener : ServerboundPacketListener, ServerPingPacketListener
{
    //HandleStatusRequest handles the status request packet
    void HandleStatusRequest(ServerboundStatusRequestPacket packet);

    //Protocol is fixed to STATUS
    ConnectionProtocol PacketListener.Protocol => ConnectionProtocol.Status;
}
