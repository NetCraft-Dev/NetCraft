namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSetStructureBlockPacket set structure block packet, maps to vanilla ServerboundSetStructureBlockPacket
//Fields: Pos(BlockPos), UpdateType(StructureBlockEntity.UpdateType), Mode(StructureMode), Name(String), Offset(BlockPos), Size(Vec3i)
public sealed record ServerboundSetStructureBlockPacket(object Pos, object UpdateType, object Mode, string Name, object Offset, object Size, object Mirror, object Rotation, string Data, bool IgnoreEntities, bool Strict, bool ShowAir, bool ShowBoundingBox, float Integrity, long Seed) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSetStructureBlockPacket> StreamCodec { get; } = new SetStructureBlockCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSetStructureBlock;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSetStructureBlock(this);

    private sealed class SetStructureBlockCodec : StreamCodec<FriendlyByteBuf, ServerboundSetStructureBlockPacket>
    {
        public ServerboundSetStructureBlockPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ServerboundSetStructureBlockPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
