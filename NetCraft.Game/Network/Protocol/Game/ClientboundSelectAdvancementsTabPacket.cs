namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSelectAdvancementsTabPacket 选择进度页签包对应原版 ClientboundSelectAdvancementsTabPacket
//字段 Tab(Identifier)
public sealed record ClientboundSelectAdvancementsTabPacket(object Tab) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSelectAdvancementsTabPacket> StreamCodec { get; } = new SelectAdvancementsTabCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSelectAdvancementsTab;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSelectAdvancementsTab(this);

    private sealed class SelectAdvancementsTabCodec : StreamCodec<FriendlyByteBuf, ClientboundSelectAdvancementsTabPacket>
    {
        public ClientboundSelectAdvancementsTabPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundSelectAdvancementsTabPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
