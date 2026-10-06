namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetHeldSlotPacket held slot packet, maps to vanilla ClientboundSetHeldSlotPacket
//Field: Slot(int)
public sealed record ClientboundSetHeldSlotPacket(int Slot) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetHeldSlotPacket> StreamCodec { get; } = new SetHeldSlotCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetHeldSlot;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetHeldSlot(this);

    private sealed class SetHeldSlotCodec : StreamCodec<FriendlyByteBuf, ClientboundSetHeldSlotPacket>
    {
        //S4 vanilla ByteBufCodecs.VAR_INT, not Byte
        public ClientboundSetHeldSlotPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ClientboundSetHeldSlotPacket value)
            => buf.WriteVarInt(value.Slot);
    }
}
