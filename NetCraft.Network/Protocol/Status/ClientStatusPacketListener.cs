using NetCraft.Network.Protocol.Ping;

namespace NetCraft.Network.Protocol.Status;

//ClientStatusPacketListener 客户端 status 监听器对应原版 net.minecraft.network.protocol.status.ClientStatusPacketListener
//继承 ClientboundPacketListener 和 ClientPongPacketListener
//Protocol 固定 STATUS
public interface ClientStatusPacketListener : ClientboundPacketListener, ClientPongPacketListener
{
    //HandleStatusResponse 处理 status 响应包
    void HandleStatusResponse(ClientboundStatusResponsePacket packet);

    //Protocol 固定为 STATUS
    ConnectionProtocol PacketListener.Protocol => ConnectionProtocol.Status;
}
