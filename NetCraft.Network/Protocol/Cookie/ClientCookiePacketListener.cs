namespace NetCraft.Network.Protocol.Cookie;

//ClientCookiePacketListener client cookie listener, maps to vanilla net.minecraft.network.protocol.cookie.ClientCookiePacketListener
//Inherits ClientboundPacketListener; Flow is fixed to CLIENTBOUND
//Protocol is fixed to CONFIGURATION
public interface ClientCookiePacketListener : ClientboundPacketListener
{
    //HandleCookieRequest handles the cookie request packet
    void HandleCookieRequest(ClientboundCookieRequestPacket packet);

    //Protocol is fixed to CONFIGURATION
    ConnectionProtocol PacketListener.Protocol => ConnectionProtocol.Configuration;
}
