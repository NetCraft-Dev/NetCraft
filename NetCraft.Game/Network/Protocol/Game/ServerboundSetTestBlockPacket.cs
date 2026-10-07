namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSetTestBlockPacket set test block packet, maps to vanilla ServerboundSetTestBlockPacket
//Fields: Position(BlockPos), Mode(TestBlockMode), Message(String)
public sealed record ServerboundSetTestBlockPacket(object Position, object Mode, string Message) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSetTestBlockPacket> StreamCodec { get; } = new SetTestBlockCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSetTestBlock;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSetTestBlock(this);

    private sealed class SetTestBlockCodec : StreamCodec<FriendlyByteBuf, ServerboundSetTestBlockPacket>
    {
        public ServerboundSetTestBlockPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ServerboundSetTestBlockPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
