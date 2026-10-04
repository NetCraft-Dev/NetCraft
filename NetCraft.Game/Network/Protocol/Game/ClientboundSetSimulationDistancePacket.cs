namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetSimulationDistancePacket 模拟距离包对应原版 ClientboundSetSimulationDistancePacket
//字段 SimulationDistance(int)
public sealed record ClientboundSetSimulationDistancePacket(int SimulationDistance) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetSimulationDistancePacket> StreamCodec { get; } = new SetSimulationDistanceCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetSimulationDistance;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetSimulationDistance(this);

    private sealed class SetSimulationDistanceCodec : StreamCodec<FriendlyByteBuf, ClientboundSetSimulationDistancePacket>
    {
        public ClientboundSetSimulationDistancePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundSetSimulationDistancePacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
