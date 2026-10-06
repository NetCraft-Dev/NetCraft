using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Common;

//ServerboundCustomClickActionPacket custom click action packet, maps to vanilla net.minecraft.network.protocol.common.ServerboundCustomClickActionPacket
//Contains Identifier id + byte[] payload; the client notifies the server of a custom click event
public sealed record ServerboundCustomClickActionPacket(Identifier Id, byte[] Payload) : Packet<ServerCommonPacketListener>
{
    public const int MaxPayloadLength = 32767;

    public static StreamCodec<FriendlyByteBuf, ServerboundCustomClickActionPacket> StreamCodec { get; } = new CustomClickCodec();

    public PacketType<ServerCommonPacketListener> Type => CommonPacketTypes.ServerboundCustomClickAction;

    public void Handle(ServerCommonPacketListener handler) => handler.HandleCustomClickAction(this);

    private sealed class CustomClickCodec : StreamCodec<FriendlyByteBuf, ServerboundCustomClickActionPacket>
    {
        public ServerboundCustomClickActionPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadIdentifier(), buf.ReadByteArray(MaxPayloadLength));

        public void Encode(FriendlyByteBuf buf, ServerboundCustomClickActionPacket value)
        {
            buf.WriteIdentifier(value.Id);
            buf.WriteByteArray(value.Payload, MaxPayloadLength);
        }
    }
}
