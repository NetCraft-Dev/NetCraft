namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundUpdateMobEffectPacket 药水效果更新包对应原版 ClientboundUpdateMobEffectPacket
//字段 EntityId(int) Effect(Holder<MobEffect>) EffectAmplifier(int) EffectDurationTicks(int) Flags(byte)
public sealed record ClientboundUpdateMobEffectPacket(int EntityId, Holder<NetCraft.Registry.MobEffect> Effect, int EffectAmplifier, int EffectDurationTicks, byte Flags) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<RegistryFriendlyByteBuf, ClientboundUpdateMobEffectPacket> StreamCodec { get; } = new UpdateMobEffectCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundUpdateMobEffect;

    public void Handle(ClientGamePacketListener handler) => handler.HandleUpdateMobEffect(this);

    //Create 按效果实例构造更新包 flags 位 ambient=1 visible=2 showIcon=4 blend=8
    public static ClientboundUpdateMobEffectPacket Create(int entityId, NetCraft.Game.World.Effect.MobEffectInstance instance, bool blend)
    {
        byte flags = 0;
        if (instance.IsAmbient) flags |= 1;
        if (instance.IsVisible) flags |= 2;
        if (instance.ShowIcon) flags |= 4;
        if (blend) flags |= 8;
        return new(entityId, instance.Effect, instance.Amplifier, instance.Duration, flags);
    }

    private sealed class UpdateMobEffectCodec : StreamCodec<RegistryFriendlyByteBuf, ClientboundUpdateMobEffectPacket>
    {
        public ClientboundUpdateMobEffectPacket Decode(RegistryFriendlyByteBuf buf)
        {
            var entityId = buf.ReadVarInt();
            var effectId = buf.ReadVarInt();
            var effect = BuiltInRegistries.MOB_EFFECT.Get(effectId)
                ?? throw new InvalidOperationException($"未知药水效果 id {effectId}");
            var amplifier = buf.ReadVarInt();
            var duration = buf.ReadVarInt();
            var flags = buf.ReadByte();
            return new(entityId, effect, amplifier, duration, flags);
        }

        public void Encode(RegistryFriendlyByteBuf buf, ClientboundUpdateMobEffectPacket value)
        {
            buf.WriteVarInt(value.EntityId);
            var id = BuiltInRegistries.MOB_EFFECT.GetId(value.Effect.Value);
            if (id < 0) throw new InvalidOperationException($"药水效果未注册: {value.Effect.Value}");
            buf.WriteVarInt(id);
            buf.WriteVarInt(value.EffectAmplifier);
            buf.WriteVarInt(value.EffectDurationTicks);
            buf.WriteByte(value.Flags);
        }
    }
}
