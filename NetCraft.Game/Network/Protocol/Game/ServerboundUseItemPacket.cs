namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundUseItemPacket 数据包对应原版 ServerboundUseItemPacket
//字段 Hand(InteractionHand) Sequence(int) YRot(float) XRot(float)
//客户端对空气按使用键时发送 与服务端 use_item_on 区分
public sealed record ServerboundUseItemPacket(InteractionHand Hand, int Sequence, float YRot, float XRot)
    : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundUseItemPacket> StreamCodec { get; } = new UseItemCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundUseItem;

    public void Handle(ServerGamePacketListener handler) => handler.HandleUseItem(this);

    private sealed class UseItemCodec : StreamCodec<FriendlyByteBuf, ServerboundUseItemPacket>
    {
        //原版 writeEnum(hand) 即 VarInt 枚举序号 后接 sequence VarInt 与两个朝向 float
        public ServerboundUseItemPacket Decode(FriendlyByteBuf buf)
        {
            var hand = buf.ReadEnum<InteractionHand>();
            var sequence = buf.ReadVarInt();
            var yRot = buf.ReadFloat();
            return new ServerboundUseItemPacket(hand, sequence, yRot, buf.ReadFloat());
        }

        public void Encode(FriendlyByteBuf buf, ServerboundUseItemPacket value)
        {
            buf.WriteEnum(value.Hand);
            buf.WriteVarInt(value.Sequence);
            buf.WriteFloat(value.YRot);
            buf.WriteFloat(value.XRot);
        }
    }
}
