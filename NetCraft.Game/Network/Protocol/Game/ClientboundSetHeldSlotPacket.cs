namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetHeldSlotPacket 持物槽包对应原版 ClientboundSetHeldSlotPacket
//字段 Slot(int)
public sealed record ClientboundSetHeldSlotPacket(int Slot) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetHeldSlotPacket> StreamCodec { get; } = new SetHeldSlotCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetHeldSlot;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetHeldSlot(this);

    private sealed class SetHeldSlotCodec : StreamCodec<FriendlyByteBuf, ClientboundSetHeldSlotPacket>
    {
        //S4 原版 ByteBufCodecs.VAR_INT 非 Byte
        public ClientboundSetHeldSlotPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ClientboundSetHeldSlotPacket value)
            => buf.WriteVarInt(value.Slot);
    }
}
