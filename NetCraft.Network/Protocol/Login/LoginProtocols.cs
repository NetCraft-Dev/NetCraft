namespace NetCraft.Network.Protocol.Login;

//LoginProtocols login 协议对应原版 net.minecraft.network.protocol.login.LoginProtocols
//注册 SERVERBOUND 和 CLIENTBOUND 协议模板顺序对齐原版
//SERVERBOUND 含 hello/key/custom_query_answer/login_acknowledged
//CLIENTBOUND 含 login_disconnect/hello/login_finished/login_compression/custom_query
//简化版不注册 cookie 子协议包
public static class LoginProtocols
{
    //ServerboundTemplate SERVERBOUND login 协议模板
    public static readonly SimpleUnboundProtocol<ServerLoginPacketListener> ServerboundTemplate =
        new ProtocolInfoBuilder<ServerLoginPacketListener>(
            ConnectionProtocol.Login, FlowDirection.Serverbound)
            .AddPacket(LoginPacketTypes.ServerboundHello, ServerboundHelloPacket.StreamCodec)
            .AddPacket(LoginPacketTypes.ServerboundKey, ServerboundKeyPacket.StreamCodec)
            .AddPacket(LoginPacketTypes.ServerboundCustomQueryAnswer, ServerboundCustomQueryAnswerPacket.StreamCodec)
            .AddPacket(LoginPacketTypes.ServerboundLoginAcknowledged, ServerboundLoginAcknowledgedPacket.StreamCodec)
            .BuildUnbound();

    //Serverbound 绑定后的 SERVERBOUND ProtocolInfo
    public static readonly ProtocolInfo<ServerLoginPacketListener> Serverbound =
        ServerboundTemplate.Bind();

    //ClientboundTemplate CLIENTBOUND login 协议模板
    public static readonly SimpleUnboundProtocol<ClientLoginPacketListener> ClientboundTemplate =
        new ProtocolInfoBuilder<ClientLoginPacketListener>(
            ConnectionProtocol.Login, FlowDirection.Clientbound)
            .AddPacket(LoginPacketTypes.ClientboundLoginDisconnect, ClientboundLoginDisconnectPacket.StreamCodec)
            .AddPacket(LoginPacketTypes.ClientboundHello, ClientboundHelloPacket.StreamCodec)
            .AddPacket(LoginPacketTypes.ClientboundLoginFinished, ClientboundLoginFinishedPacket.StreamCodec)
            .AddPacket(LoginPacketTypes.ClientboundLoginCompression, ClientboundLoginCompressionPacket.StreamCodec)
            .AddPacket(LoginPacketTypes.ClientboundCustomQuery, ClientboundCustomQueryPacket.StreamCodec)
            .BuildUnbound();

    //Clientbound 绑定后的 CLIENTBOUND ProtocolInfo
    public static readonly ProtocolInfo<ClientLoginPacketListener> Clientbound =
        ClientboundTemplate.Bind();
}
