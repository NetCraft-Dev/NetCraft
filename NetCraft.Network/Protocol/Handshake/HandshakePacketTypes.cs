using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Handshake;

//HandshakePacketTypes 握手包类型对应原版 net.minecraft.network.protocol.handshake.HandshakePacketTypes
//提供 ClientIntention 包类型注册
//所有握手包方向 SERVERBOUND
public static class HandshakePacketTypes
{
    //ClientIntention 客户端意图包类型对应原版 CLIENT_INTENTION
    //通过 PacketTypeRegistry 注册获取 int Id=0
    //Identifier 为 minecraft:intention
    public static readonly PacketType<ServerHandshakePacketListener> ClientIntention =
        PacketTypeRegistry.Register<ServerHandshakePacketListener>(
            id: 0,
            protocol: ConnectionProtocol.Handshake,
            direction: FlowDirection.Serverbound,
            codec: new WrappedCodec<ClientIntentionPacket, ServerHandshakePacketListener>(
                ClientIntentionPacket.StreamCodec))
        .WithIdentifier(Identifier.WithDefaultNamespace("intention"));
}
