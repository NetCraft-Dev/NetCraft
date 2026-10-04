namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetHealthPacket 生命值包对应原版 ClientboundSetHealthPacket
//字段 Health(float) Food(int) Saturation(float)
public sealed record ClientboundSetHealthPacket(float Health, int Food, float Saturation) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetHealthPacket> StreamCodec { get; } = new SetHealthCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetHealth;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetHealth(this);

    private sealed class SetHealthCodec : StreamCodec<FriendlyByteBuf, ClientboundSetHealthPacket>
    {
        public ClientboundSetHealthPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadFloat(), buf.ReadVarInt(), buf.ReadFloat());

        public void Encode(FriendlyByteBuf buf, ClientboundSetHealthPacket value)
        {
            buf.WriteFloat(value.Health);
            buf.WriteVarInt(value.Food);
            buf.WriteFloat(value.Saturation);
        }
    }
}
