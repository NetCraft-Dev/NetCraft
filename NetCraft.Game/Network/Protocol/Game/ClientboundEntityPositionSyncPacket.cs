using NetCraft.Primitives;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundEntityPositionSyncPacket 实体位置同步包对应原版 ClientboundEntityPositionSyncPacket
//服务端权威位置跳变走它 客户端处理时会同步重置位置基准 VecDeltaCodec
//传送包只做插值不重置基准 拿它同步大位移会让之后每个增量包都基于旧基准 偏移量固定等于这次位移
//字段 id VarInt values 位置/速度/朝向 onGround Boolean
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
