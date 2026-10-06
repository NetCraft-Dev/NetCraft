namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundRemoveMobEffectPacket remove mob effect packet, maps to vanilla ClientboundRemoveMobEffectPacket
//Fields: EntityId(int), Effect(Holder<MobEffect>)
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
                ?? throw new InvalidOperationException($"Unknown mob effect id {effectId}");
            return new(entityId, effect);
        }

        public void Encode(RegistryFriendlyByteBuf buf, ClientboundRemoveMobEffectPacket value)
        {
            buf.WriteVarInt(value.EntityId);
            var id = BuiltInRegistries.MOB_EFFECT.GetId(value.Effect.Value);
            if (id < 0) throw new InvalidOperationException($"Mob effect not registered: {value.Effect.Value}");
            buf.WriteVarInt(id);
        }
    }
}
