namespace NetCraft.Network.Protocol.Login;

//LoginProtocols login protocol, maps to vanilla net.minecraft.network.protocol.login.LoginProtocols
//Registers the SERVERBOUND and CLIENTBOUND protocol templates in vanilla order
//SERVERBOUND contains hello/key/custom_query_answer/login_acknowledged
//CLIENTBOUND contains login_disconnect/hello/login_finished/login_compression/custom_query
//The simplified form does not register cookie subprotocol packets
public static class LoginProtocols
{
    //ServerboundTemplate SERVERBOUND login protocol template
    public static readonly SimpleUnboundProtocol<ServerLoginPacketListener> ServerboundTemplate =
        new ProtocolInfoBuilder<ServerLoginPacketListener>(
            ConnectionProtocol.Login, FlowDirection.Serverbound)
            .AddPacket(LoginPacketTypes.ServerboundHello, ServerboundHelloPacket.StreamCodec)
            .AddPacket(LoginPacketTypes.ServerboundKey, ServerboundKeyPacket.StreamCodec)
            .AddPacket(LoginPacketTypes.ServerboundCustomQueryAnswer, ServerboundCustomQueryAnswerPacket.StreamCodec)
            .AddPacket(LoginPacketTypes.ServerboundLoginAcknowledged, ServerboundLoginAcknowledgedPacket.StreamCodec)
            .BuildUnbound();

    //Serverbound the bound SERVERBOUND ProtocolInfo
    public static readonly ProtocolInfo<ServerLoginPacketListener> Serverbound =
        ServerboundTemplate.Bind();

    //ClientboundTemplate CLIENTBOUND login protocol template
    public static readonly SimpleUnboundProtocol<ClientLoginPacketListener> ClientboundTemplate =
        new ProtocolInfoBuilder<ClientLoginPacketListener>(
            ConnectionProtocol.Login, FlowDirection.Clientbound)
            .AddPacket(LoginPacketTypes.ClientboundLoginDisconnect, ClientboundLoginDisconnectPacket.StreamCodec)
            .AddPacket(LoginPacketTypes.ClientboundHello, ClientboundHelloPacket.StreamCodec)
            .AddPacket(LoginPacketTypes.ClientboundLoginFinished, ClientboundLoginFinishedPacket.StreamCodec)
            .AddPacket(LoginPacketTypes.ClientboundLoginCompression, ClientboundLoginCompressionPacket.StreamCodec)
            .AddPacket(LoginPacketTypes.ClientboundCustomQuery, ClientboundCustomQueryPacket.StreamCodec)
            .BuildUnbound();

    //Clientbound the bound CLIENTBOUND ProtocolInfo
    public static readonly ProtocolInfo<ClientLoginPacketListener> Clientbound =
        ClientboundTemplate.Bind();
}
