namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundTrackedWaypointPacket 追踪航点包对应原版 ClientboundTrackedWaypointPacket
//字段 Operation(Operation) Waypoint(TrackedWaypoint)
public sealed record ClientboundTrackedWaypointPacket(object Operation, object Waypoint) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundTrackedWaypointPacket> StreamCodec { get; } = new TrackedWaypointCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundWaypoint;

    public void Handle(ClientGamePacketListener handler) => handler.HandleWaypoint(this);

    private sealed class TrackedWaypointCodec : StreamCodec<FriendlyByteBuf, ClientboundTrackedWaypointPacket>
    {
        public ClientboundTrackedWaypointPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundTrackedWaypointPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
