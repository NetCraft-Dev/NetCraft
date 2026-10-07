namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundAttackPacket attack packet, maps to vanilla ServerboundAttackPacket
//Field: EntityId(int)
public sealed record ServerboundAttackPacket(int EntityId) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundAttackPacket> StreamCodec { get; } = new AttackCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundAttack;

    public void Handle(ServerGamePacketListener handler) => handler.HandleAttack(this);

    private sealed class AttackCodec : StreamCodec<FriendlyByteBuf, ServerboundAttackPacket>
    {
        //The vanilla STREAM_CODEC has only one VAR_INT entityId
        public ServerboundAttackPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ServerboundAttackPacket value)
            => buf.WriteVarInt(value.EntityId);
    }
}
