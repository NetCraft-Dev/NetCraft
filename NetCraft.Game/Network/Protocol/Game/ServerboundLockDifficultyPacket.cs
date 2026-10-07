namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundLockDifficultyPacket lock difficulty packet, maps to vanilla ServerboundLockDifficultyPacket
//Field: Locked(boolean)
public sealed record ServerboundLockDifficultyPacket(bool Locked) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundLockDifficultyPacket> StreamCodec { get; } = new LockDifficultyCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundLockDifficulty;

    public void Handle(ServerGamePacketListener handler) => handler.HandleLockDifficulty(this);

    private sealed class LockDifficultyCodec : StreamCodec<FriendlyByteBuf, ServerboundLockDifficultyPacket>
    {
        //Sent when toggling the difficulty lock, indicating whether the whole world difficulty is locked
        public ServerboundLockDifficultyPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadBoolean());

        public void Encode(FriendlyByteBuf buf, ServerboundLockDifficultyPacket value)
            => buf.WriteBoolean(value.Locked);
    }
}
