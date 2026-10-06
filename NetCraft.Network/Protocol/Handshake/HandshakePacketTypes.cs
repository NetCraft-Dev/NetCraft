using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Handshake;

//HandshakePacketTypes handshake packet types, maps to vanilla net.minecraft.network.protocol.handshake.HandshakePacketTypes
//Provides the ClientIntention packet type registration
//All handshake packets are SERVERBOUND
public static class HandshakePacketTypes
{
    //ClientIntention client intent packet type, maps to vanilla CLIENT_INTENTION
    //Registered via PacketTypeRegistry to get int Id=0
    //Identifier is minecraft:intention
    public static readonly PacketType<ServerHandshakePacketListener> ClientIntention =
        PacketTypeRegistry.Register<ServerHandshakePacketListener>(
            id: 0,
            protocol: ConnectionProtocol.Handshake,
            direction: FlowDirection.Serverbound,
            codec: new WrappedCodec<ClientIntentionPacket, ServerHandshakePacketListener>(
                ClientIntentionPacket.StreamCodec))
        .WithIdentifier(Identifier.WithDefaultNamespace("intention"));
}
