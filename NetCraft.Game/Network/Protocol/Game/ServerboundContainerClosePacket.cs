namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundContainerClosePacket 数据包对应原版 ServerboundContainerClosePacket
//字段 ContainerId(int)
public sealed record ServerboundContainerClosePacket(int ContainerId) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundContainerClosePacket> StreamCodec { get; } = new ContainerCloseCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundContainerClose;

    public void Handle(ServerGamePacketListener handler) => handler.HandleContainerClose(this);

    private sealed class ContainerCloseCodec : StreamCodec<FriendlyByteBuf, ServerboundContainerClosePacket>
    {
        //containerId 对应原版 readContainerId/writeContainerId 均为 VarInt
        public ServerboundContainerClosePacket Decode(FriendlyByteBuf buf) => new(buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ServerboundContainerClosePacket value)
            => buf.WriteVarInt(value.ContainerId);
    }
}
