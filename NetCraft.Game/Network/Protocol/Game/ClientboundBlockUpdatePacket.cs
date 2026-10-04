using NetCraft.Primitives;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundBlockUpdatePacket 方块更新包对应原版 ClientboundBlockUpdatePacket
//字段 pos BlockPos blockState 用 BlockState registry idMapper VarInt 编解码暂用 int 占位
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
