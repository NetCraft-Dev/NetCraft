namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSetCarriedItemPacket hotbar slot switch packet, maps to vanilla ServerboundSetCarriedItemPacket
//Sent when switching the held item with the scroll wheel or number keys; field: Slot(short)
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
