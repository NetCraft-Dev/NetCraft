namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundRemoveEntitiesPacket remove entities packet, maps to vanilla ClientboundRemoveEntitiesPacket
//Field: entityIds VarInt length-prefixed int array
public sealed record ClientboundRemoveEntitiesPacket(int[] EntityIds) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundRemoveEntitiesPacket> StreamCodec { get; } = new RemoveEntitiesCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundRemoveEntities;

    public void Handle(ClientGamePacketListener handler) => handler.HandleRemoveEntities(this);

    private sealed class RemoveEntitiesCodec : StreamCodec<FriendlyByteBuf, ClientboundRemoveEntitiesPacket>
    {
        public ClientboundRemoveEntitiesPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadIntIdList());

        public void Encode(FriendlyByteBuf buf, ClientboundRemoveEntitiesPacket value)
            => buf.WriteIntIdList(value.EntityIds);
    }
}
