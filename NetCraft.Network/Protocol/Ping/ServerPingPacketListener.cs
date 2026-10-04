namespace NetCraft.Network.Protocol.Ping;

//ServerPingPacketListener 服务端 ping 监听器对应原版 net.minecraft.network.protocol.ping.ServerPingPacketListener
//继承 PacketListener 基础接口处理 ServerboundPingRequestPacket
public interface ServerPingPacketListener : PacketListener
{
    //HandlePingRequest 处理 ping 请求包
    void HandlePingRequest(ServerboundPingRequestPacket packet);
}
