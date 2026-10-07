using NetCraft.Primitives;

namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSignUpdatePacket sign update packet, maps to vanilla ServerboundSignUpdatePacket
//Fields: Pos (block position), IsFrontText(boolean), Line0-3 (four lines of text, each capped at 384)
public sealed record ServerboundSignUpdatePacket(BlockPos Pos, bool IsFrontText, string Line0, string Line1, string Line2, string Line3)
    : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSignUpdatePacket> StreamCodec { get; } = new SignUpdateCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSignUpdate;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSignUpdate(this);

    private sealed class SignUpdateCodec : StreamCodec<FriendlyByteBuf, ServerboundSignUpdatePacket>
    {
        //Sent when the sign edit screen is done; the position and front/back flag are followed by four lines of text
        public ServerboundSignUpdatePacket Decode(FriendlyByteBuf buf)
        {
            var pos = buf.ReadBlockPos();
            var isFront = buf.ReadBoolean();
            return new(pos, isFront,
                buf.ReadString(384), buf.ReadString(384), buf.ReadString(384), buf.ReadString(384));
        }

        public void Encode(FriendlyByteBuf buf, ServerboundSignUpdatePacket value)
        {
            buf.WriteBlockPos(value.Pos);
            buf.WriteBoolean(value.IsFrontText);
            buf.WriteString(value.Line0, 384);
            buf.WriteString(value.Line1, 384);
            buf.WriteString(value.Line2, 384);
            buf.WriteString(value.Line3, 384);
        }
    }
}
