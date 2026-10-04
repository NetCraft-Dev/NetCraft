namespace NetCraft.Network.Protocol.Login;

//ClientLoginPacketListener 客户端 login 监听器对应原版 net.minecraft.network.protocol.login.ClientLoginPacketListener
//继承 ClientboundPacketListener 简化版跳过 cookie 子协议
//Protocol 固定 LOGIN
public interface ClientLoginPacketListener : ClientboundPacketListener
{
    void HandleHello(ClientboundHelloPacket packet);
    void HandleLoginFinished(ClientboundLoginFinishedPacket packet);
    void HandleDisconnect(ClientboundLoginDisconnectPacket packet);
    void HandleCompression(ClientboundLoginCompressionPacket packet);
    void HandleCustomQuery(ClientboundCustomQueryPacket packet);

    //Protocol 固定为 LOGIN
    ConnectionProtocol PacketListener.Protocol => ConnectionProtocol.Login;
}
