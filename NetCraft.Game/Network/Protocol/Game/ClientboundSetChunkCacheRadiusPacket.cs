namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetChunkCacheRadiusPacket chunk cache radius packet, maps to vanilla ClientboundSetChunkCacheRadiusPacket
//Field: Radius(int)
public sealed record ClientboundSetChunkCacheRadiusPacket(int Radius) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetChunkCacheRadiusPacket> StreamCodec { get; } = new SetChunkCacheRadiusCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetChunkCacheRadius;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetChunkCacheRadius(this);

    private sealed class SetChunkCacheRadiusCodec : StreamCodec<FriendlyByteBuf, ClientboundSetChunkCacheRadiusPacket>
    {
        public ClientboundSetChunkCacheRadiusPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ClientboundSetChunkCacheRadiusPacket value)
            => buf.WriteVarInt(value.Radius);
    }
}
