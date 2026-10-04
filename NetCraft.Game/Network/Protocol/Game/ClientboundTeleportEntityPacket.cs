using NetCraft.Primitives;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundTeleportEntityPacket 实体传送包对应原版 ClientboundTeleportEntityPacket
//字段 id VarInt change 位置/速度/朝向 relatives 相对标志位掩码 int onGround Boolean
//大跨度位移用该包 原版阈值 8 格 小跨度走 ClientboundMoveEntityPacket
public sealed record ClientboundTeleportEntityPacket(int Id, Vec3 Position, Vec3 DeltaMovement,
    float YRot, float XRot, int Relatives, bool OnGround) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundTeleportEntityPacket> StreamCodec { get; } = new TeleportEntityCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundTeleportEntity;

    public void Handle(ClientGamePacketListener handler) => handler.HandleTeleportEntity(this);

    private sealed class TeleportEntityCodec : StreamCodec<FriendlyByteBuf, ClientboundTeleportEntityPacket>
    {
        public ClientboundTeleportEntityPacket Decode(FriendlyByteBuf buf)
        {
            var id = buf.ReadVarInt();
            var position = new Vec3(buf.ReadDouble(), buf.ReadDouble(), buf.ReadDouble());
            var deltaMovement = new Vec3(buf.ReadDouble(), buf.ReadDouble(), buf.ReadDouble());
            var yRot = buf.ReadFloat();
            var xRot = buf.ReadFloat();
            var relatives = buf.ReadInt();
            var onGround = buf.ReadBoolean();
            return new ClientboundTeleportEntityPacket(id, position, deltaMovement, yRot, xRot, relatives, onGround);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundTeleportEntityPacket value)
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
            buf.WriteInt(value.Relatives);
            buf.WriteBoolean(value.OnGround);
        }
    }
}
