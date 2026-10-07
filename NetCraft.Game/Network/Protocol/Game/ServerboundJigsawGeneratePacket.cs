namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundJigsawGeneratePacket jigsaw generate packet, maps to vanilla ServerboundJigsawGeneratePacket
//Fields: Pos(BlockPos), Levels(int), KeepJigsaws(boolean)
public sealed record ServerboundJigsawGeneratePacket(object Pos, int Levels, bool KeepJigsaws) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundJigsawGeneratePacket> StreamCodec { get; } = new JigsawGenerateCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundJigsawGenerate;

    public void Handle(ServerGamePacketListener handler) => handler.HandleJigsawGenerate(this);

    private sealed class JigsawGenerateCodec : StreamCodec<FriendlyByteBuf, ServerboundJigsawGeneratePacket>
    {
        public ServerboundJigsawGeneratePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ServerboundJigsawGeneratePacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
