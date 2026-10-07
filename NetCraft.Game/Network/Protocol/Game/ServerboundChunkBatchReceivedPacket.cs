namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundChunkBatchReceivedPacket chunk batch received packet, maps to vanilla ServerboundChunkBatchReceivedPacket
//Field: DesiredChunksPerTick(float)
public sealed record ServerboundChunkBatchReceivedPacket(float DesiredChunksPerTick) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundChunkBatchReceivedPacket> StreamCodec { get; } = new ChunkBatchReceivedCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundChunkBatchReceived;

    public void Handle(ServerGamePacketListener handler) => handler.HandleChunkBatchReceived(this);

    private sealed class ChunkBatchReceivedCodec : StreamCodec<FriendlyByteBuf, ServerboundChunkBatchReceivedPacket>
    {
        public ServerboundChunkBatchReceivedPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadFloat());

        public void Encode(FriendlyByteBuf buf, ServerboundChunkBatchReceivedPacket value)
            => buf.WriteFloat(value.DesiredChunksPerTick);
    }
}
