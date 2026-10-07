namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundUpdateAttributesPacket attributes update packet, maps to vanilla ClientboundUpdateAttributesPacket
//Fields: EntityId(int), Attributes(List<AttributeSnapshot>)
//On entity pairing all syncable attributes are sent; afterwards only attributes marked dirty are resent each tick
public sealed record ClientboundUpdateAttributesPacket(int EntityId, IReadOnlyList<AttributeSnapshot> Attributes)
    : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundUpdateAttributesPacket> StreamCodec { get; } = new UpdateAttributesCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundUpdateAttributes;

    public void Handle(ClientGamePacketListener handler) => handler.HandleUpdateAttributes(this);

    private sealed class UpdateAttributesCodec : StreamCodec<FriendlyByteBuf, ClientboundUpdateAttributesPacket>
    {
        public ClientboundUpdateAttributesPacket Decode(FriendlyByteBuf buf)
        {
            var entityId = buf.ReadVarInt();
            var count = buf.ReadVarInt();
            var snapshots = new AttributeSnapshot[count];
            for (var i = 0; i < count; i++) snapshots[i] = AttributeSnapshot.Read(buf);
            return new ClientboundUpdateAttributesPacket(entityId, snapshots);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundUpdateAttributesPacket value)
        {
            buf.WriteVarInt(value.EntityId);
            buf.WriteVarInt(value.Attributes.Count);
            foreach (var snapshot in value.Attributes) snapshot.Write(buf);
        }
    }
}
