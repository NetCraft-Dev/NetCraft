namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundMoveVehiclePacket move vehicle packet, maps to vanilla ClientboundMoveVehiclePacket
//Fields: Position(Vec3), YRot(float), XRot(float)
public sealed record ClientboundMoveVehiclePacket(object Position, float YRot, float XRot) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundMoveVehiclePacket> StreamCodec { get; } = new MoveVehicleCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundMoveVehicle;

    public void Handle(ClientGamePacketListener handler) => handler.HandleMoveVehicle(this);

    private sealed class MoveVehicleCodec : StreamCodec<FriendlyByteBuf, ClientboundMoveVehiclePacket>
    {
        public ClientboundMoveVehiclePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundMoveVehiclePacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
