using NetCraft.Primitives;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundBlockUpdatePacket block update packet, maps to vanilla ClientboundBlockUpdatePacket
//Fields: pos BlockPos, blockState uses the BlockState registry idMapper VarInt; for now an int placeholder is used for encoding
public sealed record ClientboundBlockUpdatePacket(BlockPos Pos, int BlockState) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundBlockUpdatePacket> StreamCodec { get; } = new BlockUpdateCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundBlockUpdate;

    public void Handle(ClientGamePacketListener handler) => handler.HandleBlockUpdate(this);

    private sealed class BlockUpdateCodec : StreamCodec<FriendlyByteBuf, ClientboundBlockUpdatePacket>
    {
        public ClientboundBlockUpdatePacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadBlockPos(), buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ClientboundBlockUpdatePacket value)
        {
            buf.WriteBlockPos(value.Pos);
            buf.WriteVarInt(value.BlockState);
        }
    }
}
