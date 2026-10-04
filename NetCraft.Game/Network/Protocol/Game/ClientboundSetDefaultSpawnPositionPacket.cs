using NetCraft.Network;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetDefaultSpawnPositionPacket 默认出生点包对应原版 ClientboundSetDefaultSpawnPositionPacket
//S4 26.2 用 LevelData.RespawnData = GlobalPos(dimension + BlockPos) + yaw(float) + pitch(float)
//Angle 字段作为 yaw 使用 pitch 固定 0
public sealed record ClientboundSetDefaultSpawnPositionPacket(BlockPos Pos, float Angle) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetDefaultSpawnPositionPacket> StreamCodec { get; } = new SetDefaultSpawnPositionCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetDefaultSpawnPosition;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetSpawn(this);

    private sealed class SetDefaultSpawnPositionCodec : StreamCodec<FriendlyByteBuf, ClientboundSetDefaultSpawnPositionPacket>
    {
        public ClientboundSetDefaultSpawnPositionPacket Decode(FriendlyByteBuf buf)
        {
            //GlobalPos: dimension resourceKey + BlockPos packed long
            buf.ReadIdentifier();
            var packed = buf.ReadLong();
            var pos = new BlockPos(BlockPos.GetX(packed), BlockPos.GetY(packed), BlockPos.GetZ(packed));
            var yaw = buf.ReadFloat();
            buf.ReadFloat();
            return new ClientboundSetDefaultSpawnPositionPacket(pos, yaw);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundSetDefaultSpawnPositionPacket value)
        {
            buf.WriteIdentifier(Identifier.WithDefaultNamespace("overworld"));
            buf.WriteLong(value.Pos.AsLong());
            buf.WriteFloat(value.Angle);
            buf.WriteFloat(0);
        }
    }
}
