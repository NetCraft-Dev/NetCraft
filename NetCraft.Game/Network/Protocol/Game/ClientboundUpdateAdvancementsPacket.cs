namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundUpdateAdvancementsPacket advancements update packet, maps to vanilla ClientboundUpdateAdvancementsPacket
//Fields: Reset(boolean), Added(List<AdvancementHolder>), Removed(Set<Identifier>), Progress(Map<Identifier, AdvancementProgress>), ShowAdvancements(boolean)
public sealed record ClientboundUpdateAdvancementsPacket(bool Reset, object Added, object Removed, object Progress, bool ShowAdvancements) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundUpdateAdvancementsPacket> StreamCodec { get; } = new UpdateAdvancementsCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundUpdateAdvancements;

    public void Handle(ClientGamePacketListener handler) => handler.HandleUpdateAdvancementsPacket(this);

    private sealed class UpdateAdvancementsCodec : StreamCodec<FriendlyByteBuf, ClientboundUpdateAdvancementsPacket>
    {
        public ClientboundUpdateAdvancementsPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundUpdateAdvancementsPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
