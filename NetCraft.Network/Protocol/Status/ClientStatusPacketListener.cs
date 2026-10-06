using NetCraft.Network.Protocol.Ping;

namespace NetCraft.Network.Protocol.Status;

//ClientStatusPacketListener client status listener, maps to vanilla net.minecraft.network.protocol.status.ClientStatusPacketListener
//Inherits ClientboundPacketListener and ClientPongPacketListener
//Protocol is fixed to STATUS
public interface ClientStatusPacketListener : ClientboundPacketListener, ClientPongPacketListener
{
    //HandleStatusResponse handles the status response packet
    void HandleStatusResponse(ClientboundStatusResponsePacket packet);

    //Protocol is fixed to STATUS
    ConnectionProtocol PacketListener.Protocol => ConnectionProtocol.Status;
}
