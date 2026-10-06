namespace NetCraft.Network.Protocol.Login;

//ClientLoginPacketListener client login listener, maps to vanilla net.minecraft.network.protocol.login.ClientLoginPacketListener
//Inherits ClientboundPacketListener; the simplified form skips the cookie subprotocol
//Protocol is fixed to LOGIN
public interface ClientLoginPacketListener : ClientboundPacketListener
{
    void HandleHello(ClientboundHelloPacket packet);
    void HandleLoginFinished(ClientboundLoginFinishedPacket packet);
    void HandleDisconnect(ClientboundLoginDisconnectPacket packet);
    void HandleCompression(ClientboundLoginCompressionPacket packet);
    void HandleCustomQuery(ClientboundCustomQueryPacket packet);

    //Protocol is fixed to LOGIN
    ConnectionProtocol PacketListener.Protocol => ConnectionProtocol.Login;
}
