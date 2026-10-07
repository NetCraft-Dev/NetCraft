namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSetCommandBlockPacket set command block packet, maps to vanilla ServerboundSetCommandBlockPacket
//Fields: Pos(BlockPos), Command(String), TrackOutput(boolean), Conditional(boolean), Automatic(boolean), Mode(CommandBlockEntity.Mode)
public sealed record ServerboundSetCommandBlockPacket(object Pos, string Command, bool TrackOutput, bool Conditional, bool Automatic, object Mode) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSetCommandBlockPacket> StreamCodec { get; } = new SetCommandBlockCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSetCommandBlock;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSetCommandBlock(this);

    private sealed class SetCommandBlockCodec : StreamCodec<FriendlyByteBuf, ServerboundSetCommandBlockPacket>
    {
        public ServerboundSetCommandBlockPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ServerboundSetCommandBlockPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
