namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundRotateHeadPacket 头部旋转包对应原版 ClientboundRotateHeadPacket
//字段 EntityId(int) YHeadRot(byte)
public sealed record ClientboundRotateHeadPacket(int EntityId, byte YHeadRot) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundRotateHeadPacket> StreamCodec { get; } = new RotateHeadCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundRotateHead;

    public void Handle(ClientGamePacketListener handler) => handler.HandleRotateMob(this);

    private sealed class RotateHeadCodec : StreamCodec<FriendlyByteBuf, ClientboundRotateHeadPacket>
    {
        //原版顺序 VAR_INT 实体id 单字节压缩角度
        public ClientboundRotateHeadPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt(), buf.ReadByte());

        public void Encode(FriendlyByteBuf buf, ClientboundRotateHeadPacket value)
        {
            buf.WriteVarInt(value.EntityId);
            buf.WriteByte(value.YHeadRot);
        }
    }
}
