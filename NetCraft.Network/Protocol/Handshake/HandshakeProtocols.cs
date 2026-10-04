namespace NetCraft.Network.Protocol.Handshake;

//HandshakeProtocols 握手协议对应原版 net.minecraft.network.protocol.handshake.HandshakeProtocols
//注册 Serverbound 握手协议模板和绑定后的 ProtocolInfo
//SERVERBOUND_TEMPLATE 未绑定模板含 ClientIntention 包
//SERVERBOUND 绑定后的 ProtocolInfo 用于编解码
public static class HandshakeProtocols
{
    //ServerboundTemplate 握手协议 SERVERBOUND 模板
    //注册 ClientIntention 包类型和编解码器
    public static readonly SimpleUnboundProtocol<ServerHandshakePacketListener> ServerboundTemplate =
        new ProtocolInfoBuilder<ServerHandshakePacketListener>(
            ConnectionProtocol.Handshake, FlowDirection.Serverbound)
            .AddPacket(HandshakePacketTypes.ClientIntention, ClientIntentionPacket.StreamCodec)
            .BuildUnbound();

    //Serverbound 绑定后的 ProtocolInfo 含 Codec 和 BundlerInfo
    public static readonly ProtocolInfo<ServerHandshakePacketListener> Serverbound =
        ServerboundTemplate.Bind();
}
