namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetActionBarTextPacket 动作栏文本包对应原版 ClientboundSetActionBarTextPacket
//字段 Text(Component)
public sealed record ClientboundSetActionBarTextPacket(Component Text) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetActionBarTextPacket> StreamCodec { get; } = new SetActionBarTextCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetActionBarText;

    public void Handle(ClientGamePacketListener handler) => handler.SetActionBarText(this);

    private sealed class SetActionBarTextCodec : StreamCodec<FriendlyByteBuf, ClientboundSetActionBarTextPacket>
    {
        public ClientboundSetActionBarTextPacket Decode(FriendlyByteBuf buf)
            => new(ComponentSerialization.StreamCodec.Decode(buf));

        public void Encode(FriendlyByteBuf buf, ClientboundSetActionBarTextPacket value)
            => ComponentSerialization.StreamCodec.Encode(buf, value.Text);
    }
}
