namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundAwardStatsPacket award stats packet, maps to vanilla ClientboundAwardStatsPacket
//Field: Stats(Object2IntMap<Stat<?>>)
public sealed record ClientboundAwardStatsPacket(object Stats) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundAwardStatsPacket> StreamCodec { get; } = new AwardStatsCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundAwardStats;

    public void Handle(ClientGamePacketListener handler) => handler.HandleAwardStats(this);

    private sealed class AwardStatsCodec : StreamCodec<FriendlyByteBuf, ClientboundAwardStatsPacket>
    {
        public ClientboundAwardStatsPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundAwardStatsPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
