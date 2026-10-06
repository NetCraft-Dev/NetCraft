namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetBorderLerpSizePacket border lerp size packet, maps to vanilla ClientboundSetBorderLerpSizePacket
//Fields: OldSize(double), NewSize(double), LerpTime(long)
public sealed record ClientboundSetBorderLerpSizePacket(double OldSize, double NewSize, long LerpTime) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetBorderLerpSizePacket> StreamCodec { get; } = new SetBorderLerpSizeCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetBorderLerpSize;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetBorderLerpSize(this);

    private sealed class SetBorderLerpSizeCodec : StreamCodec<FriendlyByteBuf, ClientboundSetBorderLerpSizePacket>
    {
        public ClientboundSetBorderLerpSizePacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadDouble(), buf.ReadDouble(), buf.ReadVarLong());

        public void Encode(FriendlyByteBuf buf, ClientboundSetBorderLerpSizePacket value)
        {
            buf.WriteDouble(value.OldSize);
            buf.WriteDouble(value.NewSize);
            buf.WriteVarLong(value.LerpTime);
        }
    }
}
