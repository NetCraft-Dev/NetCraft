namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundPlayerAbilitiesPacket 玩家能力包对应原版 ClientboundPlayerAbilitiesPacket
//字段 Invulnerable(boolean) IsFlying(boolean) CanFly(boolean) Instabuild(boolean) FlyingSpeed(float) WalkingSpeed(float)
public sealed record ClientboundPlayerAbilitiesPacket(bool Invulnerable, bool IsFlying, bool CanFly, bool Instabuild, float FlyingSpeed, float WalkingSpeed) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundPlayerAbilitiesPacket> StreamCodec { get; } = new PlayerAbilitiesCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundPlayerAbilities;

    public void Handle(ClientGamePacketListener handler) => handler.HandlePlayerAbilities(this);

    private sealed class PlayerAbilitiesCodec : StreamCodec<FriendlyByteBuf, ClientboundPlayerAbilitiesPacket>
    {
        public ClientboundPlayerAbilitiesPacket Decode(FriendlyByteBuf buf)
        {
            var flags = buf.ReadByte();
            var flyingSpeed = buf.ReadFloat();
            var walkingSpeed = buf.ReadFloat();
            return new ClientboundPlayerAbilitiesPacket(
                (flags & 1) != 0,
                (flags & 2) != 0,
                (flags & 4) != 0,
                (flags & 8) != 0,
                flyingSpeed,
                walkingSpeed);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundPlayerAbilitiesPacket value)
        {
            byte flags = 0;
            if (value.Invulnerable) flags |= 1;
            if (value.IsFlying) flags |= 2;
            if (value.CanFly) flags |= 4;
            if (value.Instabuild) flags |= 8;
            buf.WriteByte(flags);
            buf.WriteFloat(value.FlyingSpeed);
            buf.WriteFloat(value.WalkingSpeed);
        }
    }
}
