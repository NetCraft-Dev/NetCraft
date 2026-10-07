namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundContainerButtonClickPacket container button click packet, maps to vanilla ServerboundContainerButtonClickPacket
//Fields: ContainerId(int), ButtonId(int)
public sealed record ServerboundContainerButtonClickPacket(int ContainerId, int ButtonId) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundContainerButtonClickPacket> StreamCodec { get; } = new ContainerButtonClickCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundContainerButtonClick;

    public void Handle(ServerGamePacketListener handler) => handler.HandleContainerButtonClick(this);

    private sealed class ContainerButtonClickCodec : StreamCodec<FriendlyByteBuf, ServerboundContainerButtonClickPacket>
    {
        //containerId maps to vanilla readContainerId, a VarInt; buttonId is a VarInt
        public ServerboundContainerButtonClickPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt(), buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ServerboundContainerButtonClickPacket value)
        {
            buf.WriteVarInt(value.ContainerId);
            buf.WriteVarInt(value.ButtonId);
        }
    }
}
