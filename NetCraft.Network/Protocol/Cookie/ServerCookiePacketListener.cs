namespace NetCraft.Network.Protocol.Cookie;

//ServerCookiePacketListener server-side cookie listener, maps to vanilla net.minecraft.network.protocol.cookie.ServerCookiePacketListener
//Inherits ServerboundPacketListener; Flow is fixed to SERVERBOUND
//Protocol is fixed to CONFIGURATION
public interface ServerCookiePacketListener : ServerboundPacketListener
{
    //HandleCookieResponse handles the cookie response packet
    void HandleCookieResponse(ServerboundCookieResponsePacket packet);

    //Protocol is fixed to CONFIGURATION
    ConnectionProtocol PacketListener.Protocol => ConnectionProtocol.Configuration;
}
