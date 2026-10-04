namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetTitleTextPacket 标题文本包对应原版 ClientboundSetTitleTextPacket
//字段 Text(Component)
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
