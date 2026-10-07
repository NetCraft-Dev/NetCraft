namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundChangeDifficultyPacket change difficulty packet, maps to vanilla ServerboundChangeDifficultyPacket
//Field: Difficulty(int difficulty enum ordinal: 0 peaceful, 1 easy, 2 normal, 3 hard)
public sealed record ServerboundChangeDifficultyPacket(int Difficulty) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundChangeDifficultyPacket> StreamCodec { get; } = new ChangeDifficultyCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundChangeDifficulty;

    public void Handle(ServerGamePacketListener handler) => handler.HandleChangeDifficulty(this);

    private sealed class ChangeDifficultyCodec : StreamCodec<FriendlyByteBuf, ServerboundChangeDifficultyPacket>
    {
        //Sent when switching difficulty in the difficulty screen; the vanilla Difficulty enum is encoded by varint ordinal
        public ServerboundChangeDifficultyPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ServerboundChangeDifficultyPacket value)
            => buf.WriteVarInt(value.Difficulty);
    }
}
