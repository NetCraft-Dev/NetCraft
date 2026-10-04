namespace NetCraft.Network.Protocol.Handshake;

//ServerHandshakePacketListener 服务端握手包监听器对应原版 net.minecraft.network.protocol.handshake.ServerHandshakePacketListener
//继承 ServerboundPacketListener Flow 固定 SERVERBOUND
//Protocol 固定 HANDSHAKE
public interface ServerHandshakePacketListener : ServerboundPacketListener
{
    //HandleIntention 处理客户端意图包
    void HandleIntention(ClientIntentionPacket packet);

    //Protocol 固定为 HANDSHAKE
    ConnectionProtocol PacketListener.Protocol => ConnectionProtocol.Handshake;
}
