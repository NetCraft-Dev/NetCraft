namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetBorderCenterPacket border center packet, maps to vanilla ClientboundSetBorderCenterPacket
//Fields: NewCenterX(double), NewCenterZ(double)
public sealed record ClientboundSetBorderCenterPacket(double NewCenterX, double NewCenterZ) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetBorderCenterPacket> StreamCodec { get; } = new SetBorderCenterCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetBorderCenter;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetBorderCenter(this);

    private sealed class SetBorderCenterCodec : StreamCodec<FriendlyByteBuf, ClientboundSetBorderCenterPacket>
    {
        public ClientboundSetBorderCenterPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadDouble(), buf.ReadDouble());

        public void Encode(FriendlyByteBuf buf, ClientboundSetBorderCenterPacket value)
        {
            buf.WriteDouble(value.NewCenterX);
            buf.WriteDouble(value.NewCenterZ);
        }
    }
}
