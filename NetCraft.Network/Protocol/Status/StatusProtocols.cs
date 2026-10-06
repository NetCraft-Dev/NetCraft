using NetCraft.Network.Protocol.Ping;

namespace NetCraft.Network.Protocol.Status;

//StatusProtocols status protocol, maps to vanilla net.minecraft.network.protocol.status.StatusProtocols
//Vanilla registers the status request/response and ping request/pong response in the same protocol
//The ping packets are declared as Packet<ServerPingPacketListener>/Packet<ClientPongPacketListener>, aligning with vanilla Packet<? super T> contravariance
//The Status protocol bridges to ServerStatusPacketListener/ClientStatusPacketListener with AddPacketCommon
public static class StatusProtocols
{
    //ServerboundTemplate SERVERBOUND status protocol template containing the status request and ping request
    public static readonly SimpleUnboundProtocol<ServerStatusPacketListener> ServerboundTemplate =
        new ProtocolInfoBuilder<ServerStatusPacketListener>(
            ConnectionProtocol.Status, FlowDirection.Serverbound)
            .AddPacket(StatusPacketTypes.ServerboundStatusRequest, ServerboundStatusRequestPacket.StreamCodec)
            .AddPacketCommon(PingPacketTypes.ServerboundPingRequest, ServerboundPingRequestPacket.StreamCodec)
            .BuildUnbound();

    //Serverbound the bound SERVERBOUND ProtocolInfo
    public static readonly ProtocolInfo<ServerStatusPacketListener> Serverbound =
        ServerboundTemplate.Bind();

    //ClientboundTemplate CLIENTBOUND status protocol template containing the status response and pong response
    public static readonly SimpleUnboundProtocol<ClientStatusPacketListener> ClientboundTemplate =
        new ProtocolInfoBuilder<ClientStatusPacketListener>(
            ConnectionProtocol.Status, FlowDirection.Clientbound)
            .AddPacket(StatusPacketTypes.ClientboundStatusResponse, ClientboundStatusResponsePacket.StreamCodec)
            .AddPacketCommon(PingPacketTypes.ClientboundPongResponse, ClientboundPongResponsePacket.StreamCodec)
            .BuildUnbound();

    //Clientbound the bound CLIENTBOUND ProtocolInfo
    public static readonly ProtocolInfo<ClientStatusPacketListener> Clientbound =
        ClientboundTemplate.Bind();
}
