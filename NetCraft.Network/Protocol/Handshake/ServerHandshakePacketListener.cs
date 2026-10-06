namespace NetCraft.Network.Protocol.Handshake;

//ServerHandshakePacketListener server-side handshake packet listener, maps to vanilla net.minecraft.network.protocol.handshake.ServerHandshakePacketListener
//Inherits ServerboundPacketListener; Flow is fixed to SERVERBOUND
//Protocol is fixed to HANDSHAKE
public interface ServerHandshakePacketListener : ServerboundPacketListener
{
    //HandleIntention handles the client intent packet
    void HandleIntention(ClientIntentionPacket packet);

    //Protocol is fixed to HANDSHAKE
    ConnectionProtocol PacketListener.Protocol => ConnectionProtocol.Handshake;
}
