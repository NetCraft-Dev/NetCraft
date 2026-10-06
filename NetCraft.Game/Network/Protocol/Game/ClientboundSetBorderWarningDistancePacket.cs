namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetBorderWarningDistancePacket border warning distance packet, maps to vanilla ClientboundSetBorderWarningDistancePacket
//Field: WarningBlocks(int)
public sealed record ClientboundSetBorderWarningDistancePacket(int WarningBlocks) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetBorderWarningDistancePacket> StreamCodec { get; } = new SetBorderWarningDistanceCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetBorderWarningDistance;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetBorderWarningDistance(this);

    private sealed class SetBorderWarningDistanceCodec : StreamCodec<FriendlyByteBuf, ClientboundSetBorderWarningDistancePacket>
    {
        public ClientboundSetBorderWarningDistancePacket Decode(FriendlyByteBuf buf) => new(buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ClientboundSetBorderWarningDistancePacket value)
            => buf.WriteVarInt(value.WarningBlocks);
    }
}
