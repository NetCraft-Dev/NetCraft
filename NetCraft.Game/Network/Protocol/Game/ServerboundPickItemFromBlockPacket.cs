using NetCraft.Primitives;

namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundPickItemFromBlockPacket middle-click pick block packet, maps to vanilla ServerboundPickItemFromBlockPacket
//Fields: Pos(BlockPos), IncludeData(boolean, whether to include block entity data in creative mode)
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
