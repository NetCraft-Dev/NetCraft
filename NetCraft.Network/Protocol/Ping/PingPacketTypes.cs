using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Ping;

//PingPacketTypes ping packet type registration, maps to vanilla net.minecraft.network.protocol.ping.PingPacketTypes
//ClientboundPongResponse server-to-client pong response
//ServerboundPingRequest client-to-server ping request
//Handler generics use Ping's own listeners; the Status protocol registers directly and the Play protocol bridges with AddPacketCommon
public static class PingPacketTypes
{
    //ClientboundPongResponse pong response packet type minecraft:pong_response
    public static readonly PacketType<ClientPongPacketListener> ClientboundPongResponse =
        PacketTypeRegistry.Register<ClientPongPacketListener>(
            id: 1,
            protocol: ConnectionProtocol.Status,
            direction: FlowDirection.Clientbound,
            codec: new WrappedCodec<ClientboundPongResponsePacket, ClientPongPacketListener>(
                ClientboundPongResponsePacket.StreamCodec))
        .WithIdentifier(Identifier.WithDefaultNamespace("pong_response"));

    //ServerboundPingRequest ping request packet type minecraft:ping_request
    public static readonly PacketType<ServerPingPacketListener> ServerboundPingRequest =
        PacketTypeRegistry.Register<ServerPingPacketListener>(
            id: 1,
            protocol: ConnectionProtocol.Status,
            direction: FlowDirection.Serverbound,
            codec: new WrappedCodec<ServerboundPingRequestPacket, ServerPingPacketListener>(
                ServerboundPingRequestPacket.StreamCodec))
        .WithIdentifier(Identifier.WithDefaultNamespace("ping_request"));
}
