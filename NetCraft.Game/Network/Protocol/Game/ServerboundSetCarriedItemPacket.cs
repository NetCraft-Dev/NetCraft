namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSetCarriedItemPacket 切换手持栏槽位包对应原版 ServerboundSetCarriedItemPacket
//滚轮或数字键切换手持物品时发送 字段 Slot(short)
public sealed record ServerboundSetCarriedItemPacket(int Slot) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSetCarriedItemPacket> StreamCodec { get; } = new SetCarriedItemCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSetCarriedItem;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSetCarriedItem(this);

    private sealed class SetCarriedItemCodec : StreamCodec<FriendlyByteBuf, ServerboundSetCarriedItemPacket>
    {
        public ServerboundSetCarriedItemPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadShort());

        public void Encode(FriendlyByteBuf buf, ServerboundSetCarriedItemPacket value)
            => buf.WriteShort((short)value.Slot);
    }
}
