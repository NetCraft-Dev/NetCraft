using NetCraft.Network.Protocol.Cookie;

namespace NetCraft.Network.Protocol.Common;

//ServerCommonPacketListener 服务端 common 监听器对应原版 net.minecraft.network.protocol.common.ServerCommonPacketListener
//继承 ServerCookiePacketListener 加入 common 包的 6 个 handle 方法
public interface ServerCommonPacketListener : ServerCookiePacketListener
{
    void HandleClientInformation(ServerboundClientInformationPacket packet);
    void HandleCustomPayload(ServerboundCustomPayloadPacket packet);
    void HandleKeepAlive(ServerboundKeepAlivePacket packet);
    void HandlePong(ServerboundPongPacket packet);
    void HandleResourcePack(ServerboundResourcePackPacket packet);
    void HandleCustomClickAction(ServerboundCustomClickActionPacket packet);
}
