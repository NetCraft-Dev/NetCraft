namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetDisplayObjectivePacket 显示计分项包对应原版 ClientboundSetDisplayObjectivePacket
//字段 Slot(DisplaySlot) ObjectiveName(String)
public sealed record ClientboundSetDisplayObjectivePacket(object Slot, string ObjectiveName) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetDisplayObjectivePacket> StreamCodec { get; } = new SetDisplayObjectiveCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetDisplayObjective;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetDisplayObjective(this);

    private sealed class SetDisplayObjectiveCodec : StreamCodec<FriendlyByteBuf, ClientboundSetDisplayObjectivePacket>
    {
        public ClientboundSetDisplayObjectivePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundSetDisplayObjectivePacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
