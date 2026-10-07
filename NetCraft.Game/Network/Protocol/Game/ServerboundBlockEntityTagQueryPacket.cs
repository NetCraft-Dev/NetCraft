namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundBlockEntityTagQueryPacket block entity tag query packet, maps to vanilla ServerboundBlockEntityTagQueryPacket
//Fields: TransactionId(int), Pos(BlockPos)
public sealed record ServerboundBlockEntityTagQueryPacket(int TransactionId, object Pos) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundBlockEntityTagQueryPacket> StreamCodec { get; } = new BlockEntityTagQueryCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundBlockEntityTagQuery;

    public void Handle(ServerGamePacketListener handler) => handler.HandleBlockEntityTagQuery(this);

    private sealed class BlockEntityTagQueryCodec : StreamCodec<FriendlyByteBuf, ServerboundBlockEntityTagQueryPacket>
    {
        public ServerboundBlockEntityTagQueryPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ServerboundBlockEntityTagQueryPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
