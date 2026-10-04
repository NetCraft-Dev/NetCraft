namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundChunkBatchStartPacket 区块批次开始包对应原版 ClientboundChunkBatchStartPacket
//字段 
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
