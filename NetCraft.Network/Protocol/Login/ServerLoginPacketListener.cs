namespace NetCraft.Network.Protocol.Login;

//ServerLoginPacketListener server-side login listener, maps to vanilla net.minecraft.network.protocol.login.ServerLoginPacketListener
//Inherits ServerboundPacketListener; the simplified form skips the cookie subprotocol
//Protocol is fixed to LOGIN
public interface ServerLoginPacketListener : ServerboundPacketListener
{
    void HandleHello(ServerboundHelloPacket packet);
    void HandleKey(ServerboundKeyPacket packet);
    void HandleCustomQueryPacket(ServerboundCustomQueryAnswerPacket packet);
    void HandleLoginAcknowledgement(ServerboundLoginAcknowledgedPacket packet);

    //Protocol is fixed to LOGIN
    ConnectionProtocol PacketListener.Protocol => ConnectionProtocol.Login;
}
