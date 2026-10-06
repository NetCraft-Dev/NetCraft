using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Common;

//ServerboundCustomPayloadPacket server-side custom payload packet, maps to vanilla net.minecraft.network.protocol.common.ServerboundCustomPayloadPacket
//The simplified form uses Identifier id + byte[] payload to pass raw bytes through
public sealed record ServerboundCustomPayloadPacket(Identifier Id, byte[] Payload) : Packet<ServerCommonPacketListener>
{
    public const int MaxPayloadLength = 1048576;

    public static StreamCodec<FriendlyByteBuf, ServerboundCustomPayloadPacket> StreamCodec { get; } = new CustomPayloadCodec();

    public PacketType<ServerCommonPacketListener> Type => CommonPacketTypes.ServerboundCustomPayload;

    public void Handle(ServerCommonPacketListener handler) => handler.HandleCustomPayload(this);

    private sealed class CustomPayloadCodec : StreamCodec<FriendlyByteBuf, ServerboundCustomPayloadPacket>
    {
        public ServerboundCustomPayloadPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadIdentifier(), buf.ReadByteArray(MaxPayloadLength));

        public void Encode(FriendlyByteBuf buf, ServerboundCustomPayloadPacket value)
        {
            buf.WriteIdentifier(value.Id);
            buf.WriteByteArray(value.Payload, MaxPayloadLength);
        }
    }
}
