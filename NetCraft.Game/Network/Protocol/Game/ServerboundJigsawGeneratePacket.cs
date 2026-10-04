namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundJigsawGeneratePacket 数据包对应原版 ServerboundJigsawGeneratePacket
//字段 Pos(BlockPos) Levels(int) KeepJigsaws(boolean)
public sealed record ServerboundJigsawGeneratePacket(object Pos, int Levels, bool KeepJigsaws) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundJigsawGeneratePacket> StreamCodec { get; } = new JigsawGenerateCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundJigsawGenerate;

    public void Handle(ServerGamePacketListener handler) => handler.HandleJigsawGenerate(this);

    private sealed class JigsawGenerateCodec : StreamCodec<FriendlyByteBuf, ServerboundJigsawGeneratePacket>
    {
        public ServerboundJigsawGeneratePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundJigsawGeneratePacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
