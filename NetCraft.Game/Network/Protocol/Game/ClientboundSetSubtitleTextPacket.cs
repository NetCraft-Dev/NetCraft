namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetSubtitleTextPacket 副标题包对应原版 ClientboundSetSubtitleTextPacket
//字段 Text(Component)
public sealed record ClientboundSetSubtitleTextPacket(Component Text) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetSubtitleTextPacket> StreamCodec { get; } = new SetSubtitleTextCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetSubtitleText;

    public void Handle(ClientGamePacketListener handler) => handler.SetSubtitleText(this);

    private sealed class SetSubtitleTextCodec : StreamCodec<FriendlyByteBuf, ClientboundSetSubtitleTextPacket>
    {
        public ClientboundSetSubtitleTextPacket Decode(FriendlyByteBuf buf)
            => new(ComponentSerialization.StreamCodec.Decode(buf));

        public void Encode(FriendlyByteBuf buf, ClientboundSetSubtitleTextPacket value)
            => ComponentSerialization.StreamCodec.Encode(buf, value.Text);
    }
}
