namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundOpenBookPacket open book packet, maps to vanilla ClientboundOpenBookPacket
//Field: Hand(InteractionHand)
public sealed record ClientboundOpenBookPacket(object Hand) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundOpenBookPacket> StreamCodec { get; } = new OpenBookCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundOpenBook;

    public void Handle(ClientGamePacketListener handler) => handler.HandleOpenBook(this);

    private sealed class OpenBookCodec : StreamCodec<FriendlyByteBuf, ClientboundOpenBookPacket>
    {
        public ClientboundOpenBookPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundOpenBookPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
