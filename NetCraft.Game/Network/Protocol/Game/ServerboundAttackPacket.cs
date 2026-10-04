namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundAttackPacket 数据包对应原版 ServerboundAttackPacket
//字段 EntityId(int)
public sealed record ServerboundAttackPacket(int EntityId) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundAttackPacket> StreamCodec { get; } = new AttackCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundAttack;

    public void Handle(ServerGamePacketListener handler) => handler.HandleAttack(this);

    private sealed class AttackCodec : StreamCodec<FriendlyByteBuf, ServerboundAttackPacket>
    {
        //原版 STREAM_CODEC 只有一个 VAR_INT entityId
        public ServerboundAttackPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ServerboundAttackPacket value)
            => buf.WriteVarInt(value.EntityId);
    }
}
