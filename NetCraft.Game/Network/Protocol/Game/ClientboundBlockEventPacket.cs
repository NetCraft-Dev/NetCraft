using NetCraft.Primitives;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundBlockEventPacket block event packet, maps to vanilla ClientboundBlockEventPacket
//Fields: Pos (block position), B0/B1 (event params, meaning defined by the concrete block), BlockId (block registry id)
public sealed record ClientboundBlockEventPacket(BlockPos Pos, byte B0, byte B1, int BlockId)
    : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundBlockEventPacket> StreamCodec { get; } = new BlockEventCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundBlockEvent;

    public void Handle(ClientGamePacketListener handler) => handler.HandleBlockEvent(this);

    private sealed class BlockEventCodec : StreamCodec<FriendlyByteBuf, ClientboundBlockEventPacket>
    {
        //Vanilla order: writeBlockPos -> byte b0 -> byte b1 -> VarInt blockId
        public ClientboundBlockEventPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadBlockPos(), buf.ReadByte(), buf.ReadByte(), buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ClientboundBlockEventPacket value)
        {
            buf.WriteBlockPos(value.Pos);
            buf.WriteByte(value.B0);
            buf.WriteByte(value.B1);
            buf.WriteVarInt(value.BlockId);
        }
    }
}
