namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundProjectilePowerPacket projectile power packet, maps to vanilla ClientboundProjectilePowerPacket
//Fields: Id(int), AccelerationPower(double)
public sealed record ClientboundProjectilePowerPacket(int Id, double AccelerationPower) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundProjectilePowerPacket> StreamCodec { get; } = new ProjectilePowerCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundProjectilePower;

    public void Handle(ClientGamePacketListener handler) => handler.HandleProjectilePowerPacket(this);

    private sealed class ProjectilePowerCodec : StreamCodec<FriendlyByteBuf, ClientboundProjectilePowerPacket>
    {
        public ClientboundProjectilePowerPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundProjectilePowerPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
