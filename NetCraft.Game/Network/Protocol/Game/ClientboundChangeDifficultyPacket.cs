namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundChangeDifficultyPacket 难度变更包对应原版 ClientboundChangeDifficultyPacket
//字段 Difficulty(Difficulty) Locked(boolean)
public sealed record ClientboundChangeDifficultyPacket(object Difficulty, bool Locked) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundChangeDifficultyPacket> StreamCodec { get; } = new ChangeDifficultyCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundChangeDifficulty;

    public void Handle(ClientGamePacketListener handler) => handler.HandleChangeDifficulty(this);

    private sealed class ChangeDifficultyCodec : StreamCodec<FriendlyByteBuf, ClientboundChangeDifficultyPacket>
    {
        //原版顺序 无符号 byte 难度 布尔 是否锁定
        public ClientboundChangeDifficultyPacket Decode(FriendlyByteBuf buf)
            => new(World.Level.Difficulty.ById(buf.ReadByte()) ?? World.Level.Difficulty.Normal, buf.ReadBoolean());

        public void Encode(FriendlyByteBuf buf, ClientboundChangeDifficultyPacket value)
        {
            var difficulty = value.Difficulty as World.Level.Difficulty ?? World.Level.Difficulty.Normal;
            buf.WriteByte((byte)difficulty.Id);
            buf.WriteBoolean(value.Locked);
        }
    }
}
