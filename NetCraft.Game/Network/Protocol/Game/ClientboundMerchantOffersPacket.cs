namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundMerchantOffersPacket 商人交易包对应原版 ClientboundMerchantOffersPacket
//字段 ContainerId(int) Offers(MerchantOffers) VillagerLevel(int) VillagerXp(int) ShowProgress(boolean) CanRestock(boolean)
public sealed record ClientboundMerchantOffersPacket(int ContainerId, object Offers, int VillagerLevel, int VillagerXp, bool ShowProgress, bool CanRestock) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundMerchantOffersPacket> StreamCodec { get; } = new MerchantOffersCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundMerchantOffers;

    public void Handle(ClientGamePacketListener handler) => handler.HandleMerchantOffers(this);

    private sealed class MerchantOffersCodec : StreamCodec<FriendlyByteBuf, ClientboundMerchantOffersPacket>
    {
        public ClientboundMerchantOffersPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundMerchantOffersPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
