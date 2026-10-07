namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundContainerClosePacket container close packet, maps to vanilla ServerboundContainerClosePacket
//Field: ContainerId(int)
public sealed record ServerboundContainerClosePacket(int ContainerId) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundContainerClosePacket> StreamCodec { get; } = new ContainerCloseCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundContainerClose;

    public void Handle(ServerGamePacketListener handler) => handler.HandleContainerClose(this);

    private sealed class ContainerCloseCodec : StreamCodec<FriendlyByteBuf, ServerboundContainerClosePacket>
    {
        //containerId maps to vanilla readContainerId/writeContainerId, both VarInt
        public ServerboundContainerClosePacket Decode(FriendlyByteBuf buf) => new(buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ServerboundContainerClosePacket value)
            => buf.WriteVarInt(value.ContainerId);
    }
}
