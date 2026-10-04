namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundDamageEventPacket 伤害事件包对应原版 ClientboundDamageEventPacket
//字段 EntityId(int) SourceType(Holder<DamageType>) SourceCauseId(int) SourceDirectId(int) SourcePosition(Optional<Vec3>)
public sealed record ClientboundDamageEventPacket(int EntityId, object SourceType, int SourceCauseId, int SourceDirectId, object SourcePosition) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundDamageEventPacket> StreamCodec { get; } = new DamageEventCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundDamageEvent;

    public void Handle(ClientGamePacketListener handler) => handler.HandleDamageEvent(this);

    private sealed class DamageEventCodec : StreamCodec<FriendlyByteBuf, ClientboundDamageEventPacket>
    {
        public ClientboundDamageEventPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundDamageEventPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
