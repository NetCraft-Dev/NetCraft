using NetCraft.Primitives;

namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundUseItemOnPacket player using an item on a block, maps to vanilla ServerboundUseItemOnPacket
//Fields: Hand (interaction hand), BlockHit (block hit result), Sequence (block change sequence)
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
