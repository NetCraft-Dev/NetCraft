namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetEntityLinkPacket entity link packet, maps to vanilla ClientboundSetEntityLinkPacket
//Fields: SourceId(int), DestId(int)
public sealed record ClientboundSetEntityLinkPacket(int SourceId, int DestId) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetEntityLinkPacket> StreamCodec { get; } = new SetEntityLinkCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetEntityLink;

    public void Handle(ClientGamePacketListener handler) => handler.HandleEntityLinkPacket(this);

    private sealed class SetEntityLinkCodec : StreamCodec<FriendlyByteBuf, ClientboundSetEntityLinkPacket>
    {
        public ClientboundSetEntityLinkPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundSetEntityLinkPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
