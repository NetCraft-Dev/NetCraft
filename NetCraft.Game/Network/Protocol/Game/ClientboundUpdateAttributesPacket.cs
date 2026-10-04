namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundUpdateAttributesPacket 属性更新包对应原版 ClientboundUpdateAttributesPacket
//字段 EntityId(int) Attributes(List<AttributeSnapshot>)
//实体配对时下发全部可同步属性 之后每刻只补发被改脏的属性
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
