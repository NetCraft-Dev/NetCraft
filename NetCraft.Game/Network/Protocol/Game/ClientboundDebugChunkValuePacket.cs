namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundDebugChunkValuePacket 调试区块值包对应原版 ClientboundDebugChunkValuePacket
//字段 ChunkPos(ChunkPos) Update(DebugSubscription.Update<?>)
public sealed record ClientboundDebugChunkValuePacket(object ChunkPos, object Update) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundDebugChunkValuePacket> StreamCodec { get; } = new DebugChunkValueCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundDebugChunkValue;

    public void Handle(ClientGamePacketListener handler) => handler.HandleDebugChunkValue(this);

    private sealed class DebugChunkValueCodec : StreamCodec<FriendlyByteBuf, ClientboundDebugChunkValuePacket>
    {
        public ClientboundDebugChunkValuePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundDebugChunkValuePacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
