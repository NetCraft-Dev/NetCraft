namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundExplodePacket explosion packet, maps to vanilla ClientboundExplodePacket
//Fields: center Vec3 split into 3 doubles, radius float, blockCount int, playerKnockback Optional Vec3 split into hasKnockback + 3 doubles, other business types are placeholders
public sealed record ClientboundExplodePacket(double CenterX, double CenterY, double CenterZ, float Radius, int BlockCount, bool HasKnockback, double KnockbackX, double KnockbackY, double KnockbackZ, object ExplosionParticle, object ExplosionSound, object BlockParticles) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundExplodePacket> StreamCodec { get; } = new ExplodeCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundExplode;

    public void Handle(ClientGamePacketListener handler) => handler.HandleExplosion(this);

    private sealed class ExplodeCodec : StreamCodec<FriendlyByteBuf, ClientboundExplodePacket>
    {
        public ClientboundExplodePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("ParticleOptions/Holder SoundEvent/WeightedList business types not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundExplodePacket value)
            => throw new NotImplementedException("ParticleOptions/Holder SoundEvent/WeightedList business types not yet implemented");
    }
}
