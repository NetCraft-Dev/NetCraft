namespace NetCraft.Network.Protocol.Cookie;

//ClientCookiePacketListener 客户端 cookie 监听器对应原版 net.minecraft.network.protocol.cookie.ClientCookiePacketListener
//继承 ClientboundPacketListener Flow 固定 CLIENTBOUND
//Protocol 固定 CONFIGURATION
public interface ClientCookiePacketListener : ClientboundPacketListener
{
    //HandleCookieRequest 处理 cookie 请求包
    void HandleCookieRequest(ClientboundCookieRequestPacket packet);

    //Protocol 固定为 CONFIGURATION
    ConnectionProtocol PacketListener.Protocol => ConnectionProtocol.Configuration;
}
