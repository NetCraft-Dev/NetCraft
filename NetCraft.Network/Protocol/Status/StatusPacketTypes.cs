using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Status;

//StatusPacketTypes status 包类型注册对应原版 net.minecraft.network.protocol.status.StatusPacketTypes
//ClientboundStatusResponse 服务端发往客户端的 status 响应
//ServerboundStatusRequest 客户端发往服务端的 status 请求
public static class StatusPacketTypes
{
    //ClientboundStatusResponse status 响应包类型 minecraft:status_response
    public static readonly PacketType<ClientStatusPacketListener> ClientboundStatusResponse =
        PacketTypeRegistry.Register<ClientStatusPacketListener>(
            id: 0,
            protocol: ConnectionProtocol.Status,
            direction: FlowDirection.Clientbound,
            codec: new WrappedCodec<ClientboundStatusResponsePacket, ClientStatusPacketListener>(
                ClientboundStatusResponsePacket.StreamCodec))
        .WithIdentifier(Identifier.WithDefaultNamespace("status_response"));

    //ServerboundStatusRequest status 请求包类型 minecraft:status_request
    public static readonly PacketType<ServerStatusPacketListener> ServerboundStatusRequest =
        PacketTypeRegistry.Register<ServerStatusPacketListener>(
            id: 0,
            protocol: ConnectionProtocol.Status,
            direction: FlowDirection.Serverbound,
            codec: new WrappedCodec<ServerboundStatusRequestPacket, ServerStatusPacketListener>(
                ServerboundStatusRequestPacket.StreamCodec))
        .WithIdentifier(Identifier.WithDefaultNamespace("status_request"));
}
