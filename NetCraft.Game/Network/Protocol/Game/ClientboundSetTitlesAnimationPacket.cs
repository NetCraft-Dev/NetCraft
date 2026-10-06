namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetTitlesAnimationPacket titles animation packet, maps to vanilla ClientboundSetTitlesAnimationPacket
//Fields: FadeIn(int), Stay(int), FadeOut(int)
public sealed record ClientboundSetTitlesAnimationPacket(int FadeIn, int Stay, int FadeOut) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetTitlesAnimationPacket> StreamCodec { get; } = new SetTitlesAnimationCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetTitlesAnimation;

    public void Handle(ClientGamePacketListener handler) => handler.SetTitlesAnimation(this);

    private sealed class SetTitlesAnimationCodec : StreamCodec<FriendlyByteBuf, ClientboundSetTitlesAnimationPacket>
    {
        public ClientboundSetTitlesAnimationPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadInt(), buf.ReadInt(), buf.ReadInt());

        public void Encode(FriendlyByteBuf buf, ClientboundSetTitlesAnimationPacket value)
        {
            buf.WriteInt(value.FadeIn);
            buf.WriteInt(value.Stay);
            buf.WriteInt(value.FadeOut);
        }
    }
}
