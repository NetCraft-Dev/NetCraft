namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundTabListPacket Tab 列表包对应原版 ClientboundTabListPacket
//字段 Header(Component) Footer(Component)
public sealed record ClientboundTabListPacket(Component Header, Component Footer) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundTabListPacket> StreamCodec { get; } = new TabListCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundTabList;

    public void Handle(ClientGamePacketListener handler) => handler.HandleTabListCustomisation(this);

    private sealed class TabListCodec : StreamCodec<FriendlyByteBuf, ClientboundTabListPacket>
    {
        public ClientboundTabListPacket Decode(FriendlyByteBuf buf)
            => new(ComponentSerialization.StreamCodec.Decode(buf), ComponentSerialization.StreamCodec.Decode(buf));

        public void Encode(FriendlyByteBuf buf, ClientboundTabListPacket value)
        {
            ComponentSerialization.StreamCodec.Encode(buf, value.Header);
            ComponentSerialization.StreamCodec.Encode(buf, value.Footer);
        }
    }
}
