namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundHurtAnimationPacket 受伤动画包对应原版 ClientboundHurtAnimationPacket
//字段 Id(int) Yaw(float)
public sealed record ClientboundHurtAnimationPacket(int Id, float Yaw) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundHurtAnimationPacket> StreamCodec { get; } = new HurtAnimationCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundHurtAnimation;

    public void Handle(ClientGamePacketListener handler) => handler.HandleHurtAnimation(this);

    private sealed class HurtAnimationCodec : StreamCodec<FriendlyByteBuf, ClientboundHurtAnimationPacket>
    {
        public ClientboundHurtAnimationPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt(), buf.ReadFloat());

        public void Encode(FriendlyByteBuf buf, ClientboundHurtAnimationPacket value)
        {
            buf.WriteVarInt(value.Id);
            buf.WriteFloat(value.Yaw);
        }
    }
}
