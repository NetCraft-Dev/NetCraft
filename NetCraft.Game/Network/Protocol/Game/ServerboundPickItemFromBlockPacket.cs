using NetCraft.Primitives;

namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundPickItemFromBlockPacket 中键选方块包对应原版 ServerboundPickItemFromBlockPacket
//字段 Pos(BlockPos) IncludeData(boolean 创造模式是否附带方块实体数据)
public sealed record ServerboundPickItemFromBlockPacket(BlockPos Pos, bool IncludeData) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundPickItemFromBlockPacket> StreamCodec { get; } = new PickItemFromBlockCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundPickItemFromBlock;

    public void Handle(ServerGamePacketListener handler) => handler.HandlePickItemFromBlock(this);

    private sealed class PickItemFromBlockCodec : StreamCodec<FriendlyByteBuf, ServerboundPickItemFromBlockPacket>
    {
        public ServerboundPickItemFromBlockPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadBlockPos(), buf.ReadBoolean());

        public void Encode(FriendlyByteBuf buf, ServerboundPickItemFromBlockPacket value)
        {
            buf.WriteBlockPos(value.Pos);
            buf.WriteBoolean(value.IncludeData);
        }
    }
}
