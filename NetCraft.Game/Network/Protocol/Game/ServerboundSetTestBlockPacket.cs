namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSetTestBlockPacket 数据包对应原版 ServerboundSetTestBlockPacket
//字段 Position(BlockPos) Mode(TestBlockMode) Message(String)
public sealed record ServerboundSetTestBlockPacket(object Position, object Mode, string Message) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSetTestBlockPacket> StreamCodec { get; } = new SetTestBlockCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSetTestBlock;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSetTestBlock(this);

    private sealed class SetTestBlockCodec : StreamCodec<FriendlyByteBuf, ServerboundSetTestBlockPacket>
    {
        public ServerboundSetTestBlockPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundSetTestBlockPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
