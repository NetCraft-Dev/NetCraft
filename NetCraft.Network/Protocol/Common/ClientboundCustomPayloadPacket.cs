using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Common;

//ClientboundCustomPayloadPacket custom payload packet, maps to vanilla net.minecraft.network.protocol.common.ClientboundCustomPayloadPacket
//Vanilla depends on the CustomPayload helper to dispatch different payload types by id
//The simplified form uses Identifier id + byte[] payload to pass raw bytes through
//CONFIG_STREAM_CODEC and STREAM_CODEC are synonymous and share the simplified codec
public sealed record ClientboundCustomPayloadPacket(Identifier Id, byte[] Payload) : Packet<ClientCommonPacketListener>
{
    //MaxPayloadLength maximum payload length 1048576, aligns with vanilla MAX_PAYLOAD_SIZE
    public const int MaxPayloadLength = 1048576;

    //StreamCodec general-purpose packet codec
    public static StreamCodec<FriendlyByteBuf, ClientboundCustomPayloadPacket> StreamCodec { get; } = new CustomPayloadCodec();

    //ConfigStreamCodec configuration phase version, aligns with vanilla CONFIG_STREAM_CODEC
    public static StreamCodec<FriendlyByteBuf, ClientboundCustomPayloadPacket> ConfigStreamCodec => StreamCodec;

    public PacketType<ClientCommonPacketListener> Type => CommonPacketTypes.ClientboundCustomPayload;

    public void Handle(ClientCommonPacketListener handler) => handler.HandleCustomPayload(this);

    private sealed class CustomPayloadCodec : StreamCodec<FriendlyByteBuf, ClientboundCustomPayloadPacket>
    {
        public ClientboundCustomPayloadPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadIdentifier(), buf.ReadByteArray(MaxPayloadLength));

        public void Encode(FriendlyByteBuf buf, ClientboundCustomPayloadPacket value)
        {
            buf.WriteIdentifier(value.Id);
            buf.WriteByteArray(value.Payload, MaxPayloadLength);
        }
    }
}
