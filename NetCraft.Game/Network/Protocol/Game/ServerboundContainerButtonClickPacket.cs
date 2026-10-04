namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundContainerButtonClickPacket 数据包对应原版 ServerboundContainerButtonClickPacket
//字段 ContainerId(int) ButtonId(int)
public sealed record ServerboundContainerButtonClickPacket(int ContainerId, int ButtonId) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundContainerButtonClickPacket> StreamCodec { get; } = new ContainerButtonClickCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundContainerButtonClick;

    public void Handle(ServerGamePacketListener handler) => handler.HandleContainerButtonClick(this);

    private sealed class ContainerButtonClickCodec : StreamCodec<FriendlyByteBuf, ServerboundContainerButtonClickPacket>
    {
        //containerId 对应原版 readContainerId 为 VarInt buttonId 为 VarInt
        public ServerboundContainerButtonClickPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt(), buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ServerboundContainerButtonClickPacket value)
        {
            buf.WriteVarInt(value.ContainerId);
            buf.WriteVarInt(value.ButtonId);
        }
    }
}
