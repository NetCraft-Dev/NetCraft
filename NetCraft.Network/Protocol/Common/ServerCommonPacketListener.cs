using NetCraft.Network.Protocol.Cookie;

namespace NetCraft.Network.Protocol.Common;

//ServerCommonPacketListener server-side common listener, maps to vanilla net.minecraft.network.protocol.common.ServerCommonPacketListener
//Inherits ServerCookiePacketListener and adds the 6 handle methods for common packets
public interface ServerCommonPacketListener : ServerCookiePacketListener
{
    void HandleClientInformation(ServerboundClientInformationPacket packet);
    void HandleCustomPayload(ServerboundCustomPayloadPacket packet);
    void HandleKeepAlive(ServerboundKeepAlivePacket packet);
    void HandlePong(ServerboundPongPacket packet);
    void HandleResourcePack(ServerboundResourcePackPacket packet);
    void HandleCustomClickAction(ServerboundCustomClickActionPacket packet);
}
