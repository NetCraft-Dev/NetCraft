namespace NetCraft.Network.Protocol.Ping;

//ClientPongPacketListener 客户端 pong 监听器对应原版 net.minecraft.network.protocol.ping.ClientPongPacketListener
//继承 PacketListener 基础接口处理 ClientboundPongResponsePacket
public interface ClientPongPacketListener : PacketListener
{
    //HandlePongResponse 处理 pong 响应包
    void HandlePongResponse(ClientboundPongResponsePacket packet);
}
