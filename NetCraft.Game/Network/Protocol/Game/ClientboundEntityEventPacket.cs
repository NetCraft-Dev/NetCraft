namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundEntityEventPacket 实体事件包对应原版 ClientboundEntityEventPacket
//字段 EntityId(int) EventId(byte)
public sealed record ClientboundEntityEventPacket(int EntityId, byte EventId) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundEntityEventPacket> StreamCodec { get; } = new EntityEventCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundEntityEvent;

    public void Handle(ClientGamePacketListener handler) => handler.HandleEntityEvent(this);

    //原版两个字段都是定长: entityId 4 字节大端 + eventId 1 字节
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
