namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetObjectivePacket objective packet, maps to vanilla ClientboundSetObjectivePacket
//Fields: ObjectiveName(String), DisplayName(Component), RenderType(ObjectiveCriteria.RenderType), NumberFormat(Optional<NumberFormat>), Method(int)
public sealed record ClientboundSetObjectivePacket(string ObjectiveName, Component DisplayName, object RenderType, object NumberFormat, int Method) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetObjectivePacket> StreamCodec { get; } = new SetObjectiveCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetObjective;

    public void Handle(ClientGamePacketListener handler) => handler.HandleAddObjective(this);

    private sealed class SetObjectiveCodec : StreamCodec<FriendlyByteBuf, ClientboundSetObjectivePacket>
    {
        public ClientboundSetObjectivePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundSetObjectivePacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
