namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundEntityTagQueryPacket 数据包对应原版 ServerboundEntityTagQueryPacket
//字段 TransactionId(int) EntityId(int)
public sealed record ServerboundEntityTagQueryPacket(int TransactionId, int EntityId) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundEntityTagQueryPacket> StreamCodec { get; } = new EntityTagQueryCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundEntityTagQuery;

    public void Handle(ServerGamePacketListener handler) => handler.HandleEntityTagQuery(this);

    private sealed class EntityTagQueryCodec : StreamCodec<FriendlyByteBuf, ServerboundEntityTagQueryPacket>
    {
        public ServerboundEntityTagQueryPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundEntityTagQueryPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
