namespace NetCraft.Network.Protocol.Cookie;

//ServerCookiePacketListener 服务端 cookie 监听器对应原版 net.minecraft.network.protocol.cookie.ServerCookiePacketListener
//继承 ServerboundPacketListener Flow 固定 SERVERBOUND
//Protocol 固定 CONFIGURATION
public interface ServerCookiePacketListener : ServerboundPacketListener
{
    //HandleCookieResponse 处理 cookie 响应包
    void HandleCookieResponse(ServerboundCookieResponsePacket packet);

    //Protocol 固定为 CONFIGURATION
    ConnectionProtocol PacketListener.Protocol => ConnectionProtocol.Configuration;
}
