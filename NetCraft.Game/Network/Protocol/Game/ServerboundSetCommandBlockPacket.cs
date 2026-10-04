namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSetCommandBlockPacket 数据包对应原版 ServerboundSetCommandBlockPacket
//字段 Pos(BlockPos) Command(String) TrackOutput(boolean) Conditional(boolean) Automatic(boolean) Mode(CommandBlockEntity.Mode)
public sealed record ServerboundSetCommandBlockPacket(object Pos, string Command, bool TrackOutput, bool Conditional, bool Automatic, object Mode) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSetCommandBlockPacket> StreamCodec { get; } = new SetCommandBlockCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSetCommandBlock;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSetCommandBlock(this);

    private sealed class SetCommandBlockCodec : StreamCodec<FriendlyByteBuf, ServerboundSetCommandBlockPacket>
    {
        public ServerboundSetCommandBlockPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundSetCommandBlockPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
