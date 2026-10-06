namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundDebugChunkValuePacket debug chunk value packet, maps to vanilla ClientboundDebugChunkValuePacket
//Fields: ChunkPos(ChunkPos), Update(DebugSubscription.Update<?>)
public sealed record ClientboundDebugChunkValuePacket(object ChunkPos, object Update) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundDebugChunkValuePacket> StreamCodec { get; } = new DebugChunkValueCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundDebugChunkValue;

    public void Handle(ClientGamePacketListener handler) => handler.HandleDebugChunkValue(this);

    private sealed class DebugChunkValueCodec : StreamCodec<FriendlyByteBuf, ClientboundDebugChunkValuePacket>
    {
        public ClientboundDebugChunkValuePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundDebugChunkValuePacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
