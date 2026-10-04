using NetCraft.Primitives;

namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundUseItemOnPacket 玩家对方块使用物品包对应原版 ServerboundUseItemOnPacket
//字段 Hand(交互手) BlockHit(方块命中结果) Sequence(方块变更序号)
public sealed record ServerboundUseItemOnPacket(InteractionHand Hand, BlockHitResult BlockHit, int Sequence)
    : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundUseItemOnPacket> StreamCodec { get; } = new UseItemOnCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundUseItemOn;

    public void Handle(ServerGamePacketListener handler) => handler.HandleUseItemOn(this);

    private sealed class UseItemOnCodec : StreamCodec<FriendlyByteBuf, ServerboundUseItemOnPacket>
    {
        public ServerboundUseItemOnPacket Decode(FriendlyByteBuf buf)
        {
            var hand = buf.ReadEnum<InteractionHand>();
            var blockHit = buf.ReadBlockHitResult();
            return new ServerboundUseItemOnPacket(hand, blockHit, buf.ReadVarInt());
        }

        public void Encode(FriendlyByteBuf buf, ServerboundUseItemOnPacket value)
        {
            buf.WriteEnum(value.Hand);
            buf.WriteBlockHitResult(value.BlockHit);
            buf.WriteVarInt(value.Sequence);
        }
    }
}
