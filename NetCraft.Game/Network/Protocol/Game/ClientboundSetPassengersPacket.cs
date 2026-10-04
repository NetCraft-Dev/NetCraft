namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetPassengersPacket 乘客设置包对应原版 ClientboundSetPassengersPacket
//字段 Vehicle(int)
public sealed record ClientboundSetPassengersPacket(int Vehicle) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetPassengersPacket> StreamCodec { get; } = new SetPassengersCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetPassengers;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetEntityPassengersPacket(this);

    private sealed class SetPassengersCodec : StreamCodec<FriendlyByteBuf, ClientboundSetPassengersPacket>
    {
        public ClientboundSetPassengersPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundSetPassengersPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
