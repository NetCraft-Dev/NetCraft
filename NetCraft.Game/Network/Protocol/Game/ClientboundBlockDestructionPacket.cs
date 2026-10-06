using NetCraft.Primitives;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundBlockDestructionPacket block destruction progress packet, maps to vanilla ClientboundBlockDestructionPacket
//Fields: id VarInt, pos BlockPos, progress UnsignedByte
public sealed record ClientboundBlockDestructionPacket(int Id, BlockPos Pos, int Progress) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundBlockDestructionPacket> StreamCodec { get; } = new BlockDestructionCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundBlockDestruction;

    public void Handle(ClientGamePacketListener handler) => handler.HandleBlockDestruction(this);

    private sealed class BlockDestructionCodec : StreamCodec<FriendlyByteBuf, ClientboundBlockDestructionPacket>
    {
        public ClientboundBlockDestructionPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt(), buf.ReadBlockPos(), buf.ReadUnsignedByte());

        public void Encode(FriendlyByteBuf buf, ClientboundBlockDestructionPacket value)
        {
            buf.WriteVarInt(value.Id);
            buf.WriteBlockPos(value.Pos);
            buf.WriteByte((byte)value.Progress);
        }
    }
}
