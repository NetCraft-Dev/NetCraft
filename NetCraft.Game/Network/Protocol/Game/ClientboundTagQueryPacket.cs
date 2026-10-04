namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundTagQueryPacket 标签查询包对应原版 ClientboundTagQueryPacket
//字段 TransactionId(int) Tag(CompoundTag)
public sealed record ClientboundTagQueryPacket(int TransactionId, object Tag) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundTagQueryPacket> StreamCodec { get; } = new TagQueryCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundTagQuery;

    public void Handle(ClientGamePacketListener handler) => handler.HandleTagQueryPacket(this);

    private sealed class TagQueryCodec : StreamCodec<FriendlyByteBuf, ClientboundTagQueryPacket>
    {
        public ClientboundTagQueryPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundTagQueryPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
