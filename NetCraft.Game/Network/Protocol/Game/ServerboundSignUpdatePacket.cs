using NetCraft.Primitives;

namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSignUpdatePacket 数据包对应原版 ServerboundSignUpdatePacket
//字段 Pos(方块坐标) IsFrontText(boolean) Line0-3(四行文本 每行上限 384)
public sealed record ServerboundSignUpdatePacket(BlockPos Pos, bool IsFrontText, string Line0, string Line1, string Line2, string Line3)
    : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSignUpdatePacket> StreamCodec { get; } = new SignUpdateCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSignUpdate;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSignUpdate(this);

    private sealed class SignUpdateCodec : StreamCodec<FriendlyByteBuf, ServerboundSignUpdatePacket>
    {
        //告示牌编辑界面完成时发送 坐标与正反面标志后跟四行文本
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
