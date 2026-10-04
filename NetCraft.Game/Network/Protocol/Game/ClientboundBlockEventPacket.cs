using NetCraft.Primitives;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundBlockEventPacket 方块事件包对应原版 ClientboundBlockEventPacket
//字段 Pos(方块坐标) B0/B1(事件参数 含义由具体方块定义) BlockId(方块注册表序号)
public sealed record ClientboundBlockEventPacket(BlockPos Pos, byte B0, byte B1, int BlockId)
    : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundBlockEventPacket> StreamCodec { get; } = new BlockEventCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundBlockEvent;

    public void Handle(ClientGamePacketListener handler) => handler.HandleBlockEvent(this);

    private sealed class BlockEventCodec : StreamCodec<FriendlyByteBuf, ClientboundBlockEventPacket>
    {
        //原版顺序 writeBlockPos -> byte b0 -> byte b1 -> VarInt blockId
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
