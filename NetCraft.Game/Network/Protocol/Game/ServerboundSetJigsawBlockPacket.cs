namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSetJigsawBlockPacket set jigsaw block packet, maps to vanilla ServerboundSetJigsawBlockPacket
//Fields: Pos(BlockPos), Name(Identifier), Target(Identifier), Pool(Identifier), FinalState(String), Joint(JigsawBlockEntity.JointType)
public sealed record ServerboundSetJigsawBlockPacket(object Pos, object Name, object Target, object Pool, string FinalState, object Joint, int SelectionPriority, int PlacementPriority) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSetJigsawBlockPacket> StreamCodec { get; } = new SetJigsawBlockCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSetJigsawBlock;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSetJigsawBlock(this);

    private sealed class SetJigsawBlockCodec : StreamCodec<FriendlyByteBuf, ServerboundSetJigsawBlockPacket>
    {
        public ServerboundSetJigsawBlockPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ServerboundSetJigsawBlockPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
