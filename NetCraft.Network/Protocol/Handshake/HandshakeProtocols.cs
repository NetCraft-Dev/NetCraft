namespace NetCraft.Network.Protocol.Handshake;

//HandshakeProtocols handshake protocol, maps to vanilla net.minecraft.network.protocol.handshake.HandshakeProtocols
//Registers the Serverbound handshake protocol template and the bound ProtocolInfo
//SERVERBOUND_TEMPLATE unbound template containing the ClientIntention packet
//SERVERBOUND the bound ProtocolInfo used for coding
public static class HandshakeProtocols
{
    //ServerboundTemplate handshake protocol SERVERBOUND template
    //Registers the ClientIntention packet type and codec
    public static readonly SimpleUnboundProtocol<ServerHandshakePacketListener> ServerboundTemplate =
        new ProtocolInfoBuilder<ServerHandshakePacketListener>(
            ConnectionProtocol.Handshake, FlowDirection.Serverbound)
            .AddPacket(HandshakePacketTypes.ClientIntention, ClientIntentionPacket.StreamCodec)
            .BuildUnbound();

    //Serverbound the bound ProtocolInfo containing the Codec and BundlerInfo
    public static readonly ProtocolInfo<ServerHandshakePacketListener> Serverbound =
        ServerboundTemplate.Bind();
}
