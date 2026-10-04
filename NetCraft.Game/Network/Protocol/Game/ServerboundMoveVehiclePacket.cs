namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundMoveVehiclePacket 数据包对应原版 ServerboundMoveVehiclePacket
//字段 X/Y/Z(double 载具坐标 原版 Vec3 即三个 double) YRot(float) XRot(float) OnGround(boolean)
public sealed record ServerboundMoveVehiclePacket(double X, double Y, double Z, float YRot, float XRot, bool OnGround) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundMoveVehiclePacket> StreamCodec { get; } = new MoveVehicleCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundMoveVehicle;

    public void Handle(ServerGamePacketListener handler) => handler.HandleMoveVehicle(this);

    private sealed class MoveVehicleCodec : StreamCodec<FriendlyByteBuf, ServerboundMoveVehiclePacket>
    {
        //玩家驾驶载具时每刻上报位姿 坐标三个 double 后接两个浮点角度与着地标志
        public ServerboundMoveVehiclePacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadDouble(), buf.ReadDouble(), buf.ReadDouble(),
                buf.ReadFloat(), buf.ReadFloat(), buf.ReadBoolean());

        public void Encode(FriendlyByteBuf buf, ServerboundMoveVehiclePacket value)
        {
            buf.WriteDouble(value.X);
            buf.WriteDouble(value.Y);
            buf.WriteDouble(value.Z);
            buf.WriteFloat(value.YRot);
            buf.WriteFloat(value.XRot);
            buf.WriteBoolean(value.OnGround);
        }
    }
}
