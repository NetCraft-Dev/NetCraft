namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundEntityEventPacket entity event packet, maps to vanilla ClientboundEntityEventPacket
//Fields: EntityId(int), EventId(byte)
public sealed record ClientboundEntityEventPacket(int EntityId, byte EventId) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundEntityEventPacket> StreamCodec { get; } = new EntityEventCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundEntityEvent;

    public void Handle(ClientGamePacketListener handler) => handler.HandleEntityEvent(this);

    //Both vanilla fields are fixed-length: entityId 4-byte big-endian + eventId 1 byte
    private sealed class EntityEventCodec : StreamCodec<FriendlyByteBuf, ClientboundEntityEventPacket>
    {
        public ClientboundEntityEventPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadInt(), buf.ReadByte());

        public void Encode(FriendlyByteBuf buf, ClientboundEntityEventPacket value)
        {
            buf.WriteInt(value.EntityId);
            buf.WriteByte(value.EventId);
        }
    }
}
