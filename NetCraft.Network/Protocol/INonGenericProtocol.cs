namespace NetCraft.Network.Protocol;

//INonGenericProtocol non-generic protocol interface
//Connection stores the current inbound/outbound protocol without depending on THandler, passing Packet boxed as object
//ProtocolInfo<THandler> inherits this interface and provides a non-generic access point via explicit implementation
public interface INonGenericProtocol
{
    //Id protocol enum
    ConnectionProtocol Id { get; }

    //FlowDirection packet direction
    FlowDirection FlowDirection { get; }

    //DecodePacket decodes one packet from the buffer and returns Packet<THandler> boxed as object
    object? DecodePacket(RegistryFriendlyByteBuf buf);

    //EncodePacket encodes one packet, taking Packet<THandler> boxed as object
    void EncodePacket(RegistryFriendlyByteBuf buf, object packet);

    //PacketIdFor gets the packet's network ID written to the buffer prefix
    int PacketIdFor(object packet);
}
