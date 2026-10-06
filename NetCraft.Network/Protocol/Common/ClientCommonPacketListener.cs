using NetCraft.Network.Protocol.Cookie;

namespace NetCraft.Network.Protocol.Common;

//ClientCommonPacketListener client common listener, maps to vanilla net.minecraft.network.protocol.common.ClientCommonPacketListener
//Inherits ClientCookiePacketListener and adds the 13 handle methods for common packets
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
