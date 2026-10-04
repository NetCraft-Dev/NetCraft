namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundOpenBookPacket 打开书本包对应原版 ClientboundOpenBookPacket
//字段 Hand(InteractionHand)
public sealed record ClientboundOpenBookPacket(object Hand) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundOpenBookPacket> StreamCodec { get; } = new OpenBookCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundOpenBook;

    public void Handle(ClientGamePacketListener handler) => handler.HandleOpenBook(this);

    private sealed class OpenBookCodec : StreamCodec<FriendlyByteBuf, ClientboundOpenBookPacket>
    {
        public ClientboundOpenBookPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundOpenBookPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
