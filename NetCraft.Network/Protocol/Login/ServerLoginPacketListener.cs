namespace NetCraft.Network.Protocol.Login;

//ServerLoginPacketListener 服务端 login 监听器对应原版 net.minecraft.network.protocol.login.ServerLoginPacketListener
//继承 ServerboundPacketListener 简化版跳过 cookie 子协议
//Protocol 固定 LOGIN
public interface ServerLoginPacketListener : ServerboundPacketListener
{
    void HandleHello(ServerboundHelloPacket packet);
    void HandleKey(ServerboundKeyPacket packet);
    void HandleCustomQueryPacket(ServerboundCustomQueryAnswerPacket packet);
    void HandleLoginAcknowledgement(ServerboundLoginAcknowledgedPacket packet);

    //Protocol 固定为 LOGIN
    ConnectionProtocol PacketListener.Protocol => ConnectionProtocol.Login;
}
