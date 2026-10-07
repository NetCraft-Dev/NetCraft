namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundEntityTagQueryPacket entity tag query packet, maps to vanilla ServerboundEntityTagQueryPacket
//Fields: TransactionId(int), EntityId(int)
public sealed record ServerboundEntityTagQueryPacket(int TransactionId, int EntityId) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundEntityTagQueryPacket> StreamCodec { get; } = new EntityTagQueryCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundEntityTagQuery;

    public void Handle(ServerGamePacketListener handler) => handler.HandleEntityTagQuery(this);

    private sealed class EntityTagQueryCodec : StreamCodec<FriendlyByteBuf, ServerboundEntityTagQueryPacket>
    {
        public ServerboundEntityTagQueryPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ServerboundEntityTagQueryPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
