namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetEntityLinkPacket 实体链接包对应原版 ClientboundSetEntityLinkPacket
//字段 SourceId(int) DestId(int)
public sealed record ClientboundSetEntityLinkPacket(int SourceId, int DestId) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetEntityLinkPacket> StreamCodec { get; } = new SetEntityLinkCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetEntityLink;

    public void Handle(ClientGamePacketListener handler) => handler.HandleEntityLinkPacket(this);

    private sealed class SetEntityLinkCodec : StreamCodec<FriendlyByteBuf, ClientboundSetEntityLinkPacket>
    {
        public ClientboundSetEntityLinkPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundSetEntityLinkPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
