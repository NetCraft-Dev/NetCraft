using NetCraft.Primitives;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundTeleportEntityPacket teleport entity packet, maps to vanilla ClientboundTeleportEntityPacket
//Fields: id VarInt, change position/velocity/rotation, relatives relative-flag bitmask int, onGround Boolean
//A large displacement uses this packet (vanilla threshold 8 blocks); a small displacement uses ClientboundMoveEntityPacket
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
