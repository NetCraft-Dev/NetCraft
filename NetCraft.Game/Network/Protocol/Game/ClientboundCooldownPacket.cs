namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundCooldownPacket item cooldown packet, maps to vanilla ClientboundCooldownPacket
//Fields: CooldownGroup(Identifier), Duration(int)
public sealed record ClientboundCooldownPacket(object CooldownGroup, int Duration) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundCooldownPacket> StreamCodec { get; } = new CooldownCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundCooldown;

    public void Handle(ClientGamePacketListener handler) => handler.HandleItemCooldown(this);

    private sealed class CooldownCodec : StreamCodec<FriendlyByteBuf, ClientboundCooldownPacket>
    {
        public ClientboundCooldownPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundCooldownPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
