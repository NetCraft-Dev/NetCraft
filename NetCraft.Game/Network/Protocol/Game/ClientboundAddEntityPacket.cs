using NetCraft.Game.World.Entity;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundAddEntityPacket 添加实体包对应原版 ClientboundAddEntityPacket
//字段 id VarInt uuid UUID type EntityType 网络序号 VarInt x/y/z 3 double movement LpVec3 xRot/yRot/yHeadRot 3 byte data VarInt
public sealed record ClientboundAddEntityPacket(int Id, Guid Uuid, EntityType<object> Kind,
    double X, double Y, double Z, Vec3 Movement, byte XRot, byte YRot, byte YHeadRot, int Data)
    : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundAddEntityPacket> StreamCodec { get; } = new AddEntityCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundAddEntity;

    public void Handle(ClientGamePacketListener handler) => handler.HandleAddEntity(this);

    private sealed class AddEntityCodec : StreamCodec<FriendlyByteBuf, ClientboundAddEntityPacket>
    {
        public ClientboundAddEntityPacket Decode(FriendlyByteBuf buf)
        {
            var id = buf.ReadVarInt();
            var uuid = buf.ReadUuid();
            var rawType = buf.ReadVarInt();
            //未注册类型给占位而不是抛异常 否则整包被丢 已登记的实体在客户端直接消失
            var kind = EntityTypes.ByIdOrUnknown(rawType);
            var x = buf.ReadDouble();
            var y = buf.ReadDouble();
            var z = buf.ReadDouble();
            var movement = buf.ReadLpVec3();
            var xRot = buf.ReadByte();
            var yRot = buf.ReadByte();
            var yHeadRot = buf.ReadByte();
            var data = buf.ReadVarInt();
            return new ClientboundAddEntityPacket(id, uuid, kind, x, y, z, movement, xRot, yRot, yHeadRot, data);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundAddEntityPacket value)
        {
            buf.WriteVarInt(value.Id);
            buf.WriteUuid(value.Uuid);
            buf.WriteVarInt(value.Kind.RawId);
            buf.WriteDouble(value.X);
            buf.WriteDouble(value.Y);
            buf.WriteDouble(value.Z);
            buf.WriteLpVec3(value.Movement);
            buf.WriteByte(value.XRot);
            buf.WriteByte(value.YRot);
            buf.WriteByte(value.YHeadRot);
            buf.WriteVarInt(value.Data);
        }
    }
}
