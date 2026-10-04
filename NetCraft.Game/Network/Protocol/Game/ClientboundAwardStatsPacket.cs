namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundAwardStatsPacket 统计数据包对应原版 ClientboundAwardStatsPacket
//字段 Stats(Object2IntMap<Stat<?>>)
public sealed record ClientboundAwardStatsPacket(object Stats) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundAwardStatsPacket> StreamCodec { get; } = new AwardStatsCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundAwardStats;

    public void Handle(ClientGamePacketListener handler) => handler.HandleAwardStats(this);

    private sealed class AwardStatsCodec : StreamCodec<FriendlyByteBuf, ClientboundAwardStatsPacket>
    {
        public ClientboundAwardStatsPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundAwardStatsPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
