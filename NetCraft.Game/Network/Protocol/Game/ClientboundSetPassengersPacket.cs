namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetPassengersPacket set passengers packet, maps to vanilla ClientboundSetPassengersPacket
//Field: Vehicle(int)
public sealed record ClientboundSetPassengersPacket(int Vehicle) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetPassengersPacket> StreamCodec { get; } = new SetPassengersCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetPassengers;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetEntityPassengersPacket(this);

    private sealed class SetPassengersCodec : StreamCodec<FriendlyByteBuf, ClientboundSetPassengersPacket>
    {
        public ClientboundSetPassengersPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundSetPassengersPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
