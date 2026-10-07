namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundUseItemPacket use item packet, maps to vanilla ServerboundUseItemPacket
//Fields: Hand(InteractionHand), Sequence(int), YRot(float), XRot(float)
//Sent when the client presses the use key into the air, distinct from the serverbound use_item_on
public sealed record ServerboundUseItemPacket(InteractionHand Hand, int Sequence, float YRot, float XRot)
    : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundUseItemPacket> StreamCodec { get; } = new UseItemCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundUseItem;

    public void Handle(ServerGamePacketListener handler) => handler.HandleUseItem(this);

    private sealed class UseItemCodec : StreamCodec<FriendlyByteBuf, ServerboundUseItemPacket>
    {
        //Vanilla writeEnum(hand), i.e. a VarInt enum ordinal, followed by sequence VarInt and two rotation floats
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
