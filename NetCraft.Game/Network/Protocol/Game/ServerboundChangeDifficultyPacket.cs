namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundChangeDifficultyPacket 数据包对应原版 ServerboundChangeDifficultyPacket
//字段 Difficulty(int 难度枚举序号 0和平1简单2普通3困难)
public sealed record ServerboundChangeDifficultyPacket(int Difficulty) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundChangeDifficultyPacket> StreamCodec { get; } = new ChangeDifficultyCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundChangeDifficulty;

    public void Handle(ServerGamePacketListener handler) => handler.HandleChangeDifficulty(this);

    private sealed class ChangeDifficultyCodec : StreamCodec<FriendlyByteBuf, ServerboundChangeDifficultyPacket>
    {
        //难度界面切换难度时发送 原版 Difficulty 枚举按 varint 序号编码
        public ServerboundChangeDifficultyPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ServerboundChangeDifficultyPacket value)
            => buf.WriteVarInt(value.Difficulty);
    }
}
