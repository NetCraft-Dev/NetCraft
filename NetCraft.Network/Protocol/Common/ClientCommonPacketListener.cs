using NetCraft.Network.Protocol.Cookie;

namespace NetCraft.Network.Protocol.Common;

//ClientCommonPacketListener 客户端 common 监听器对应原版 net.minecraft.network.protocol.common.ClientCommonPacketListener
//继承 ClientCookiePacketListener 加入 common 包的 13 个 handle 方法
public interface ClientCommonPacketListener : ClientCookiePacketListener
{
    void HandleKeepAlive(ClientboundKeepAlivePacket packet);
    void HandlePing(ClientboundPingPacket packet);
    void HandleCustomPayload(ClientboundCustomPayloadPacket packet);
    void HandleDisconnect(ClientboundDisconnectPacket packet);
    void HandleResourcePackPush(ClientboundResourcePackPushPacket packet);
    void HandleResourcePackPop(ClientboundResourcePackPopPacket packet);
    void HandleUpdateTags(ClientboundUpdateTagsPacket packet);
    void HandleStoreCookie(ClientboundStoreCookiePacket packet);
    void HandleTransfer(ClientboundTransferPacket packet);
    void HandleCustomReportDetails(ClientboundCustomReportDetailsPacket packet);
    void HandleServerLinks(ClientboundServerLinksPacket packet);
    void HandleClearDialog(ClientboundClearDialogPacket packet);
    void HandleShowDialog(ClientboundShowDialogPacket packet);
}
