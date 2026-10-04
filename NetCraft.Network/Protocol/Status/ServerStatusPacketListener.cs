using NetCraft.Network.Protocol.Ping;

namespace NetCraft.Network.Protocol.Status;

//ServerStatusPacketListener 服务端 status 监听器对应原版 net.minecraft.network.protocol.status.ServerStatusPacketListener
//继承 ServerboundPacketListener 和 ServerPingPacketListener
//Protocol 固定 STATUS
public interface ServerStatusPacketListener : ServerboundPacketListener, ServerPingPacketListener
{
    //HandleStatusRequest 处理 status 请求包
    void HandleStatusRequest(ServerboundStatusRequestPacket packet);

    //Protocol 固定为 STATUS
    ConnectionProtocol PacketListener.Protocol => ConnectionProtocol.Status;
}
