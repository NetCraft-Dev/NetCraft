namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundDamageEventPacket damage event packet, maps to vanilla ClientboundDamageEventPacket
//Fields: EntityId(int), SourceType(Holder<DamageType>), SourceCauseId(int), SourceDirectId(int), SourcePosition(Optional<Vec3>)
public sealed record ClientboundDamageEventPacket(int EntityId, object SourceType, int SourceCauseId, int SourceDirectId, object SourcePosition) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundDamageEventPacket> StreamCodec { get; } = new DamageEventCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundDamageEvent;

    public void Handle(ClientGamePacketListener handler) => handler.HandleDamageEvent(this);

    private sealed class DamageEventCodec : StreamCodec<FriendlyByteBuf, ClientboundDamageEventPacket>
    {
        public ClientboundDamageEventPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundDamageEventPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
