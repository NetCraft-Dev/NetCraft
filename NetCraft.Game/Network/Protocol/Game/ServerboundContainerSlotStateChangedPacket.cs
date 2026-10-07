namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundContainerSlotStateChangedPacket container slot state changed packet, maps to vanilla ServerboundContainerSlotStateChangedPacket
//Fields: SlotId(int), ContainerId(int), NewState(boolean)
public sealed record ServerboundContainerSlotStateChangedPacket(int SlotId, int ContainerId, bool NewState) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundContainerSlotStateChangedPacket> StreamCodec { get; } = new ContainerSlotStateChangedCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundContainerSlotStateChanged;

    public void Handle(ServerGamePacketListener handler) => handler.HandleContainerSlotStateChanged(this);

    private sealed class ContainerSlotStateChangedCodec : StreamCodec<FriendlyByteBuf, ServerboundContainerSlotStateChangedPacket>
    {
        //Sent when a container slot's toggle state changes; slotId and containerId are both VarInt
        public ServerboundContainerSlotStateChangedPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt(), buf.ReadVarInt(), buf.ReadBoolean());

        public void Encode(FriendlyByteBuf buf, ServerboundContainerSlotStateChangedPacket value)
        {
            buf.WriteVarInt(value.SlotId);
            buf.WriteVarInt(value.ContainerId);
            buf.WriteBoolean(value.NewState);
        }
    }
}
