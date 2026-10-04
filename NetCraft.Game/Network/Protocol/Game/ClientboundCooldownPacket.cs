namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundCooldownPacket 物品冷却包对应原版 ClientboundCooldownPacket
//字段 CooldownGroup(Identifier) Duration(int)
public sealed record ClientboundCooldownPacket(object CooldownGroup, int Duration) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundCooldownPacket> StreamCodec { get; } = new CooldownCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundCooldown;

    public void Handle(ClientGamePacketListener handler) => handler.HandleItemCooldown(this);

    private sealed class CooldownCodec : StreamCodec<FriendlyByteBuf, ClientboundCooldownPacket>
    {
        public ClientboundCooldownPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundCooldownPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
