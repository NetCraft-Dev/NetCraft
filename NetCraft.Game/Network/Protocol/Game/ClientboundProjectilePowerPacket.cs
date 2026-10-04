namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundProjectilePowerPacket 抛射物力量包对应原版 ClientboundProjectilePowerPacket
//字段 Id(int) AccelerationPower(double)
public sealed record ClientboundProjectilePowerPacket(int Id, double AccelerationPower) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundProjectilePowerPacket> StreamCodec { get; } = new ProjectilePowerCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundProjectilePower;

    public void Handle(ClientGamePacketListener handler) => handler.HandleProjectilePowerPacket(this);

    private sealed class ProjectilePowerCodec : StreamCodec<FriendlyByteBuf, ClientboundProjectilePowerPacket>
    {
        public ClientboundProjectilePowerPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundProjectilePowerPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
