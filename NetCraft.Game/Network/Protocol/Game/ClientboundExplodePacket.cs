namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundExplodePacket 爆炸包对应原版 ClientboundExplodePacket
//字段 center Vec3 拆 3 double radius float blockCount int playerKnockback Optional Vec3 拆 hasKnockback + 3 double 其他业务类型占位
public sealed record ClientboundExplodePacket(double CenterX, double CenterY, double CenterZ, float Radius, int BlockCount, bool HasKnockback, double KnockbackX, double KnockbackY, double KnockbackZ, object ExplosionParticle, object ExplosionSound, object BlockParticles) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundExplodePacket> StreamCodec { get; } = new ExplodeCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundExplode;

    public void Handle(ClientGamePacketListener handler) => handler.HandleExplosion(this);

    private sealed class ExplodeCodec : StreamCodec<FriendlyByteBuf, ClientboundExplodePacket>
    {
        public ClientboundExplodePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("ParticleOptions/Holder SoundEvent/WeightedList 业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundExplodePacket value)
            => throw new NotImplementedException("ParticleOptions/Holder SoundEvent/WeightedList 业务类型待实现");
    }
}
