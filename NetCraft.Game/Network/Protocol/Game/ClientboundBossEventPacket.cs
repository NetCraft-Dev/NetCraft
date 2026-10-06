namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundBossEventPacket boss bar event packet, maps to vanilla ClientboundBossEventPacket
//Fields: id UUID, operation Operation business type placeholder
public sealed record ClientboundBossEventPacket(Guid Id, object Operation) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundBossEventPacket> StreamCodec { get; } = new BossEventCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundBossEvent;

    public void Handle(ClientGamePacketListener handler) => handler.HandleBossUpdate(this);

    private sealed class BossEventCodec : StreamCodec<FriendlyByteBuf, ClientboundBossEventPacket>
    {
        public ClientboundBossEventPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("BossEvent.Operation business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundBossEventPacket value)
            => throw new NotImplementedException("BossEvent.Operation business type not yet implemented");
    }
}
