namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundLockDifficultyPacket 数据包对应原版 ServerboundLockDifficultyPacket
//字段 Locked(boolean)
public sealed record ServerboundLockDifficultyPacket(bool Locked) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundLockDifficultyPacket> StreamCodec { get; } = new LockDifficultyCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundLockDifficulty;

    public void Handle(ServerGamePacketListener handler) => handler.HandleLockDifficulty(this);

    private sealed class LockDifficultyCodec : StreamCodec<FriendlyByteBuf, ServerboundLockDifficultyPacket>
    {
        //难度锁定开关切换时发送 表示是否锁定整个世界难度
        public ServerboundLockDifficultyPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadBoolean());

        public void Encode(FriendlyByteBuf buf, ServerboundLockDifficultyPacket value)
            => buf.WriteBoolean(value.Locked);
    }
}
