namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundChangeDifficultyPacket difficulty change packet, maps to vanilla ClientboundChangeDifficultyPacket
//Fields: Difficulty(Difficulty), Locked(boolean)
public sealed record ClientboundChangeDifficultyPacket(object Difficulty, bool Locked) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundChangeDifficultyPacket> StreamCodec { get; } = new ChangeDifficultyCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundChangeDifficulty;

    public void Handle(ClientGamePacketListener handler) => handler.HandleChangeDifficulty(this);

    private sealed class ChangeDifficultyCodec : StreamCodec<FriendlyByteBuf, ClientboundChangeDifficultyPacket>
    {
        //Vanilla order: unsigned byte difficulty, boolean locked
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
