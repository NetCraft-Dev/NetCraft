namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundChunkBatchStartPacket chunk batch start packet, maps to vanilla ClientboundChunkBatchStartPacket
//Fields:
public sealed record ClientboundChunkBatchStartPacket() : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundChunkBatchStartPacket> StreamCodec { get; } = new ChunkBatchStartCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundChunkBatchStart;

    public void Handle(ClientGamePacketListener handler) => handler.HandleChunkBatchStart(this);

    private sealed class ChunkBatchStartCodec : StreamCodec<FriendlyByteBuf, ClientboundChunkBatchStartPacket>
    {
        public ClientboundChunkBatchStartPacket Decode(FriendlyByteBuf buf)
            => new();

        public void Encode(FriendlyByteBuf buf, ClientboundChunkBatchStartPacket value)
            { }
    }
}
