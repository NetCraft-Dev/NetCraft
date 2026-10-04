namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundChunkBatchFinishedPacket 区块批次结束包对应原版 ClientboundChunkBatchFinishedPacket
//字段 BatchSize(int)
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
