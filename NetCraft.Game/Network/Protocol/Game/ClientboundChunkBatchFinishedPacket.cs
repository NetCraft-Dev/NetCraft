namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundChunkBatchFinishedPacket chunk batch finished packet, maps to vanilla ClientboundChunkBatchFinishedPacket
//Field: BatchSize(int)
public sealed record ClientboundChunkBatchFinishedPacket(int BatchSize) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundChunkBatchFinishedPacket> StreamCodec { get; } = new ChunkBatchFinishedCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundChunkBatchFinished;

    public void Handle(ClientGamePacketListener handler) => handler.HandleChunkBatchFinished(this);

    private sealed class ChunkBatchFinishedCodec : StreamCodec<FriendlyByteBuf, ClientboundChunkBatchFinishedPacket>
    {
        public ClientboundChunkBatchFinishedPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ClientboundChunkBatchFinishedPacket value)
            => buf.WriteVarInt(value.BatchSize);
    }
}
