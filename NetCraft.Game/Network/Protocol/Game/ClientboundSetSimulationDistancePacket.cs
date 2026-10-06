namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetSimulationDistancePacket simulation distance packet, maps to vanilla ClientboundSetSimulationDistancePacket
//Field: SimulationDistance(int)
public sealed record ClientboundSetSimulationDistancePacket(int SimulationDistance) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetSimulationDistancePacket> StreamCodec { get; } = new SetSimulationDistanceCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetSimulationDistance;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetSimulationDistance(this);

    private sealed class SetSimulationDistanceCodec : StreamCodec<FriendlyByteBuf, ClientboundSetSimulationDistancePacket>
    {
        public ClientboundSetSimulationDistancePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundSetSimulationDistancePacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
