namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundRemoveMobEffectPacket 移除药水效果包对应原版 ClientboundRemoveMobEffectPacket
//字段 EntityId(int) Effect(Holder<MobEffect>)
public sealed record ClientboundRemoveMobEffectPacket(int EntityId, Holder<NetCraft.Registry.MobEffect> Effect) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<RegistryFriendlyByteBuf, ClientboundRemoveMobEffectPacket> StreamCodec { get; } = new RemoveMobEffectCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundRemoveMobEffect;

    public void Handle(ClientGamePacketListener handler) => handler.HandleRemoveMobEffect(this);

    private sealed class RemoveMobEffectCodec : StreamCodec<RegistryFriendlyByteBuf, ClientboundRemoveMobEffectPacket>
    {
        public ClientboundRemoveMobEffectPacket Decode(RegistryFriendlyByteBuf buf)
        {
            var entityId = buf.ReadVarInt();
            var effectId = buf.ReadVarInt();
            var effect = BuiltInRegistries.MOB_EFFECT.Get(effectId)
                ?? throw new InvalidOperationException($"未知药水效果 id {effectId}");
            return new(entityId, effect);
        }

        public void Encode(RegistryFriendlyByteBuf buf, ClientboundRemoveMobEffectPacket value)
        {
            buf.WriteVarInt(value.EntityId);
            var id = BuiltInRegistries.MOB_EFFECT.GetId(value.Effect.Value);
            if (id < 0) throw new InvalidOperationException($"药水效果未注册: {value.Effect.Value}");
            buf.WriteVarInt(id);
        }
    }
}
