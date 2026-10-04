namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundContainerSlotStateChangedPacket 数据包对应原版 ServerboundContainerSlotStateChangedPacket
//字段 SlotId(int) ContainerId(int) NewState(boolean)
public sealed record ServerboundContainerSlotStateChangedPacket(int SlotId, int ContainerId, bool NewState) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundContainerSlotStateChangedPacket> StreamCodec { get; } = new ContainerSlotStateChangedCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundContainerSlotStateChanged;

    public void Handle(ServerGamePacketListener handler) => handler.HandleContainerSlotStateChanged(this);

    private sealed class ContainerSlotStateChangedCodec : StreamCodec<FriendlyByteBuf, ServerboundContainerSlotStateChangedPacket>
    {
        //集装箱槽位开关状态变化时发送 slotId 与 containerId 都是 VarInt
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
