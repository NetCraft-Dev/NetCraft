namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSelectAdvancementsTabPacket select advancements tab packet, maps to vanilla ClientboundSelectAdvancementsTabPacket
//Field: Tab(Identifier)
public sealed record ClientboundSelectAdvancementsTabPacket(object Tab) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSelectAdvancementsTabPacket> StreamCodec { get; } = new SelectAdvancementsTabCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSelectAdvancementsTab;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSelectAdvancementsTab(this);

    private sealed class SelectAdvancementsTabCodec : StreamCodec<FriendlyByteBuf, ClientboundSelectAdvancementsTabPacket>
    {
        public ClientboundSelectAdvancementsTabPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundSelectAdvancementsTabPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
