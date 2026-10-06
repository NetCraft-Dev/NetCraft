namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundContainerClosePacket container close packet, maps to vanilla ClientboundContainerClosePacket
//Field: ContainerId(int)
public sealed record ClientboundContainerClosePacket(int ContainerId) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundContainerClosePacket> StreamCodec { get; } = new ContainerCloseCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundContainerClose;

    public void Handle(ClientGamePacketListener handler) => handler.HandleContainerClose(this);

    private sealed class ContainerCloseCodec : StreamCodec<FriendlyByteBuf, ClientboundContainerClosePacket>
    {
        public ClientboundContainerClosePacket Decode(FriendlyByteBuf buf) => new(buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ClientboundContainerClosePacket value)
            => buf.WriteVarInt(value.ContainerId);
    }
}
