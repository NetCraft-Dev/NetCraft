namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundOpenScreenPacket open screen packet, maps to vanilla ClientboundOpenScreenPacket
//Fields: ContainerId(int), Type(MenuType), Title(Component)
//StreamCodec uses ContainerId(VarInt) + MenuType.StreamCodec + ComponentSerialization.StreamCodec
public sealed record ClientboundOpenScreenPacket(int ContainerId, MenuType Kind, Component Title) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<RegistryFriendlyByteBuf, ClientboundOpenScreenPacket> StreamCodec { get; } = new OpenScreenCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundOpenScreen;

    public void Handle(ClientGamePacketListener handler) => handler.HandleOpenScreen(this);

    private sealed class OpenScreenCodec : StreamCodec<RegistryFriendlyByteBuf, ClientboundOpenScreenPacket>
    {
        public ClientboundOpenScreenPacket Decode(RegistryFriendlyByteBuf buf)
            => new(buf.ReadVarInt(), MenuType.StreamCodec.Decode(buf), ComponentSerialization.StreamCodec.Decode(buf));

        public void Encode(RegistryFriendlyByteBuf buf, ClientboundOpenScreenPacket value)
        {
            buf.WriteVarInt(value.ContainerId);
            MenuType.StreamCodec.Encode(buf, value.Kind);
            ComponentSerialization.StreamCodec.Encode(buf, value.Title);
        }
    }
}
