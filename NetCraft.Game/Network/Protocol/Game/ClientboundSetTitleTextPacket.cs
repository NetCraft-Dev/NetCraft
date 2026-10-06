namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetTitleTextPacket title text packet, maps to vanilla ClientboundSetTitleTextPacket
//Field: Text(Component)
public sealed record ClientboundSetTitleTextPacket(Component Text) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetTitleTextPacket> StreamCodec { get; } = new SetTitleTextCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetTitleText;

    public void Handle(ClientGamePacketListener handler) => handler.SetTitleText(this);

    private sealed class SetTitleTextCodec : StreamCodec<FriendlyByteBuf, ClientboundSetTitleTextPacket>
    {
        public ClientboundSetTitleTextPacket Decode(FriendlyByteBuf buf)
            => new(ComponentSerialization.StreamCodec.Decode(buf));

        public void Encode(FriendlyByteBuf buf, ClientboundSetTitleTextPacket value)
            => ComponentSerialization.StreamCodec.Encode(buf, value.Text);
    }
}
