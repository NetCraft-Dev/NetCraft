using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Common;

//ServerboundCustomClickActionPacket 自定义点击动作包对应原版 net.minecraft.network.protocol.common.ServerboundCustomClickActionPacket
//含 Identifier id + byte[] payload 客户端通知服务端自定义点击事件
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
