namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundRotateHeadPacket rotate head packet, maps to vanilla ClientboundRotateHeadPacket
//Fields: EntityId(int), YHeadRot(byte)
public sealed record ClientboundRotateHeadPacket(int EntityId, byte YHeadRot) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundRotateHeadPacket> StreamCodec { get; } = new RotateHeadCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundRotateHead;

    public void Handle(ClientGamePacketListener handler) => handler.HandleRotateMob(this);

    private sealed class RotateHeadCodec : StreamCodec<FriendlyByteBuf, ClientboundRotateHeadPacket>
    {
        //Vanilla order: VAR_INT entity id, single-byte packed angle
        public ClientboundRotateHeadPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt(), buf.ReadByte());

        public void Encode(FriendlyByteBuf buf, ClientboundRotateHeadPacket value)
        {
            buf.WriteVarInt(value.EntityId);
            buf.WriteByte(value.YHeadRot);
        }
    }
}
