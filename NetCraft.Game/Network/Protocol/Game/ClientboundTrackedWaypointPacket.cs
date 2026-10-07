namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundTrackedWaypointPacket tracked waypoint packet, maps to vanilla ClientboundTrackedWaypointPacket
//Fields: Operation(Operation), Waypoint(TrackedWaypoint)
public sealed record ClientboundTrackedWaypointPacket(object Operation, object Waypoint) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundTrackedWaypointPacket> StreamCodec { get; } = new TrackedWaypointCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundWaypoint;

    public void Handle(ClientGamePacketListener handler) => handler.HandleWaypoint(this);

    private sealed class TrackedWaypointCodec : StreamCodec<FriendlyByteBuf, ClientboundTrackedWaypointPacket>
    {
        public ClientboundTrackedWaypointPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundTrackedWaypointPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
