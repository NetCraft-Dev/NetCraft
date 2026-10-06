namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundBlockChangedAckPacket block changed ack packet, maps to vanilla ClientboundBlockChangedAckPacket
//Field: Sequence(int)
public sealed record ClientboundBlockChangedAckPacket(int Sequence) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundBlockChangedAckPacket> StreamCodec { get; } = new BlockChangedAckCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundBlockChangedAck;

    public void Handle(ClientGamePacketListener handler) => handler.HandleBlockChangedAck(this);

    //Vanilla sequence is a VarInt
    private sealed class BlockChangedAckCodec : StreamCodec<FriendlyByteBuf, ClientboundBlockChangedAckPacket>
    {
        public ClientboundBlockChangedAckPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ClientboundBlockChangedAckPacket value)
            => buf.WriteVarInt(value.Sequence);
    }
}
