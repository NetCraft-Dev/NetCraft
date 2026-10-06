namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundTagQueryPacket tag query packet, maps to vanilla ClientboundTagQueryPacket
//Fields: TransactionId(int), Tag(CompoundTag)
public sealed record ClientboundTagQueryPacket(int TransactionId, object Tag) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundTagQueryPacket> StreamCodec { get; } = new TagQueryCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundTagQuery;

    public void Handle(ClientGamePacketListener handler) => handler.HandleTagQueryPacket(this);

    private sealed class TagQueryCodec : StreamCodec<FriendlyByteBuf, ClientboundTagQueryPacket>
    {
        public ClientboundTagQueryPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundTagQueryPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
