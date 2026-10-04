namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetChunkCacheCenterPacket 区块缓存中心包对应原版 ClientboundSetChunkCacheCenterPacket
//字段 X(int) Z(int)
public sealed record ClientboundSetChunkCacheCenterPacket(int X, int Z) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetChunkCacheCenterPacket> StreamCodec { get; } = new SetChunkCacheCenterCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetChunkCacheCenter;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetChunkCacheCenter(this);

    private sealed class SetChunkCacheCenterCodec : StreamCodec<FriendlyByteBuf, ClientboundSetChunkCacheCenterPacket>
    {
        public ClientboundSetChunkCacheCenterPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt(), buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ClientboundSetChunkCacheCenterPacket value)
        {
            buf.WriteVarInt(value.X);
            buf.WriteVarInt(value.Z);
        }
    }
}
