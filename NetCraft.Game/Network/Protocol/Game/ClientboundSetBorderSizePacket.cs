namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetBorderSizePacket border size packet, maps to vanilla ClientboundSetBorderSizePacket
//Field: Size(double)
public sealed record ClientboundSetBorderSizePacket(double Size) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetBorderSizePacket> StreamCodec { get; } = new SetBorderSizeCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetBorderSize;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetBorderSize(this);

    private sealed class SetBorderSizeCodec : StreamCodec<FriendlyByteBuf, ClientboundSetBorderSizePacket>
    {
        public ClientboundSetBorderSizePacket Decode(FriendlyByteBuf buf) => new(buf.ReadDouble());

        public void Encode(FriendlyByteBuf buf, ClientboundSetBorderSizePacket value)
            => buf.WriteDouble(value.Size);
    }
}
