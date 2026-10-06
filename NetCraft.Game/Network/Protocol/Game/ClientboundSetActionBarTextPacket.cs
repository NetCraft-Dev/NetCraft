namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetActionBarTextPacket action bar text packet, maps to vanilla ClientboundSetActionBarTextPacket
//Field: Text(Component)
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
