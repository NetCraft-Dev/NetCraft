using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Ping;

//PingPacketTypes ping 包类型注册对应原版 net.minecraft.network.protocol.ping.PingPacketTypes
//ClientboundPongResponse 服务端发往客户端的 pong 响应
//ServerboundPingRequest 客户端发往服务端的 ping 请求
//Handler 泛型用 Ping 自己的监听器 Status 协议直接注册 Play 协议用 AddPacketCommon 桥接
public static class PingPacketTypes
{
    //ClientboundPongResponse pong 响应包类型 minecraft:pong_response
    public static readonly PacketType<ClientPongPacketListener> ClientboundPongResponse =
        PacketTypeRegistry.Register<ClientPongPacketListener>(
            id: 1,
            protocol: ConnectionProtocol.Status,
            direction: FlowDirection.Clientbound,
            codec: new WrappedCodec<ClientboundPongResponsePacket, ClientPongPacketListener>(
                ClientboundPongResponsePacket.StreamCodec))
        .WithIdentifier(Identifier.WithDefaultNamespace("pong_response"));

    //ServerboundPingRequest ping 请求包类型 minecraft:ping_request
    public static readonly PacketType<ServerPingPacketListener> ServerboundPingRequest =
        PacketTypeRegistry.Register<ServerPingPacketListener>(
            id: 1,
            protocol: ConnectionProtocol.Status,
            direction: FlowDirection.Serverbound,
            codec: new WrappedCodec<ServerboundPingRequestPacket, ServerPingPacketListener>(
                ServerboundPingRequestPacket.StreamCodec))
        .WithIdentifier(Identifier.WithDefaultNamespace("ping_request"));
}
