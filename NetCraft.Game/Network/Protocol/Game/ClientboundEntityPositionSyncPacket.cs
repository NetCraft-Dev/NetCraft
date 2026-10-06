using NetCraft.Primitives;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundEntityPositionSyncPacket entity position sync packet, maps to vanilla ClientboundEntityPositionSyncPacket
//Used for authoritative server position jumps; the client resets its position baseline in sync via VecDeltaCodec
//The teleport packet only interpolates and does not reset the baseline; using it for a large displacement makes every later delta packet use the old baseline, with the offset permanently equal to that displacement
//Fields: id VarInt, values position/velocity/rotation, onGround Boolean
public sealed record ClientboundEntityPositionSyncPacket(int Id, Vec3 Position, Vec3 DeltaMovement,
    float YRot, float XRot, bool OnGround) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundEntityPositionSyncPacket> StreamCodec { get; } = new EntityPositionSyncCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundEntityPositionSync;

    public void Handle(ClientGamePacketListener handler) => handler.HandleEntityPositionSync(this);

    private sealed class EntityPositionSyncCodec : StreamCodec<FriendlyByteBuf, ClientboundEntityPositionSyncPacket>
    {
        public ClientboundEntityPositionSyncPacket Decode(FriendlyByteBuf buf)
        {
            var id = buf.ReadVarInt();
            var position = new Vec3(buf.ReadDouble(), buf.ReadDouble(), buf.ReadDouble());
            var deltaMovement = new Vec3(buf.ReadDouble(), buf.ReadDouble(), buf.ReadDouble());
            var yRot = buf.ReadFloat();
            var xRot = buf.ReadFloat();
            var onGround = buf.ReadBoolean();
            return new ClientboundEntityPositionSyncPacket(id, position, deltaMovement, yRot, xRot, onGround);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundEntityPositionSyncPacket value)
        {
            buf.WriteVarInt(value.Id);
            buf.WriteDouble(value.Position.X);
            buf.WriteDouble(value.Position.Y);
            buf.WriteDouble(value.Position.Z);
            buf.WriteDouble(value.DeltaMovement.X);
            buf.WriteDouble(value.DeltaMovement.Y);
            buf.WriteDouble(value.DeltaMovement.Z);
            buf.WriteFloat(value.YRot);
            buf.WriteFloat(value.XRot);
            buf.WriteBoolean(value.OnGround);
        }
    }
}
