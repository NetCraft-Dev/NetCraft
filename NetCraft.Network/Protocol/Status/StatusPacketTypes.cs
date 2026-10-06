using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Status;

//StatusPacketTypes status packet type registration, maps to vanilla net.minecraft.network.protocol.status.StatusPacketTypes
//ClientboundStatusResponse server-to-client status response
//ServerboundStatusRequest client-to-server status request
public static class StatusPacketTypes
{
    //ClientboundStatusResponse status response packet type minecraft:status_response
    public static readonly PacketType<ClientStatusPacketListener> ClientboundStatusResponse =
        PacketTypeRegistry.Register<ClientStatusPacketListener>(
            id: 0,
            protocol: ConnectionProtocol.Status,
            direction: FlowDirection.Clientbound,
            codec: new WrappedCodec<ClientboundStatusResponsePacket, ClientStatusPacketListener>(
                ClientboundStatusResponsePacket.StreamCodec))
        .WithIdentifier(Identifier.WithDefaultNamespace("status_response"));

    //ServerboundStatusRequest status request packet type minecraft:status_request
    public static readonly PacketType<ServerStatusPacketListener> ServerboundStatusRequest =
        PacketTypeRegistry.Register<ServerStatusPacketListener>(
            id: 0,
            protocol: ConnectionProtocol.Status,
            direction: FlowDirection.Serverbound,
            codec: new WrappedCodec<ServerboundStatusRequestPacket, ServerStatusPacketListener>(
                ServerboundStatusRequestPacket.StreamCodec))
        .WithIdentifier(Identifier.WithDefaultNamespace("status_request"));
}
