namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundMerchantOffersPacket merchant offers packet, maps to vanilla ClientboundMerchantOffersPacket
//Fields: ContainerId(int), Offers(MerchantOffers), VillagerLevel(int), VillagerXp(int), ShowProgress(boolean), CanRestock(boolean)
public sealed record ClientboundMerchantOffersPacket(int ContainerId, object Offers, int VillagerLevel, int VillagerXp, bool ShowProgress, bool CanRestock) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundMerchantOffersPacket> StreamCodec { get; } = new MerchantOffersCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundMerchantOffers;

    public void Handle(ClientGamePacketListener handler) => handler.HandleMerchantOffers(this);

    private sealed class MerchantOffersCodec : StreamCodec<FriendlyByteBuf, ClientboundMerchantOffersPacket>
    {
        public ClientboundMerchantOffersPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundMerchantOffersPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
