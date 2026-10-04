using NetCraft.Game.World.Particle;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundLevelParticlesPacket 世界粒子包对应原版 ClientboundLevelParticlesPacket
//字段顺序与原版 write 一致 overrideLimiter/alwaysShow 两个布尔在前 坐标与散布其后 最后是粒子选项
public sealed record ClientboundLevelParticlesPacket(double X, double Y, double Z, float XDist, float YDist, float ZDist, float MaxSpeed, int Count, bool OverrideLimiter, bool AlwaysShow, ParticleOptions Particle) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<RegistryFriendlyByteBuf, ClientboundLevelParticlesPacket> StreamCodec { get; } = new LevelParticlesCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundLevelParticles;

    public void Handle(ClientGamePacketListener handler) => handler.HandleParticleEvent(this);

    private sealed class LevelParticlesCodec : StreamCodec<RegistryFriendlyByteBuf, ClientboundLevelParticlesPacket>
    {
        public ClientboundLevelParticlesPacket Decode(RegistryFriendlyByteBuf buf)
        {
            var overrideLimiter = buf.ReadBoolean();
            var alwaysShow = buf.ReadBoolean();
            var x = buf.ReadDouble();
            var y = buf.ReadDouble();
            var z = buf.ReadDouble();
            var xDist = buf.ReadFloat();
            var yDist = buf.ReadFloat();
            var zDist = buf.ReadFloat();
            var maxSpeed = buf.ReadFloat();
            var count = buf.ReadInt();
            var particle = ParticleTypes.StreamCodec.Decode(buf);
            return new(x, y, z, xDist, yDist, zDist, maxSpeed, count, overrideLimiter, alwaysShow, particle);
        }

        public void Encode(RegistryFriendlyByteBuf buf, ClientboundLevelParticlesPacket value)
        {
            buf.WriteBoolean(value.OverrideLimiter);
            buf.WriteBoolean(value.AlwaysShow);
            buf.WriteDouble(value.X);
            buf.WriteDouble(value.Y);
            buf.WriteDouble(value.Z);
            buf.WriteFloat(value.XDist);
            buf.WriteFloat(value.YDist);
            buf.WriteFloat(value.ZDist);
            buf.WriteFloat(value.MaxSpeed);
            buf.WriteInt(value.Count);
            ParticleTypes.StreamCodec.Encode(buf, value.Particle);
        }
    }
}
