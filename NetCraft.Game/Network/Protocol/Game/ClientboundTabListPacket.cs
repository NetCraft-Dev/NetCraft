namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundTabListPacket tab list packet, maps to vanilla ClientboundTabListPacket
//Fields: Header(Component), Footer(Component)
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
