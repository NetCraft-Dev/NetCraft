namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetObjectivePacket 计分项包对应原版 ClientboundSetObjectivePacket
//字段 ObjectiveName(String) DisplayName(Component) RenderType(ObjectiveCriteria.RenderType) NumberFormat(Optional<NumberFormat>) Method(int)
public sealed record ClientboundSetObjectivePacket(string ObjectiveName, Component DisplayName, object RenderType, object NumberFormat, int Method) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetObjectivePacket> StreamCodec { get; } = new SetObjectiveCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetObjective;

    public void Handle(ClientGamePacketListener handler) => handler.HandleAddObjective(this);

    private sealed class SetObjectiveCodec : StreamCodec<FriendlyByteBuf, ClientboundSetObjectivePacket>
    {
        public ClientboundSetObjectivePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundSetObjectivePacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
