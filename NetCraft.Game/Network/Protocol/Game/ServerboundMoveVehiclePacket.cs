namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundMoveVehiclePacket move vehicle packet, maps to vanilla ServerboundMoveVehiclePacket
//Fields: X/Y/Z (double vehicle position, the vanilla Vec3 is three doubles), YRot(float), XRot(float), OnGround(boolean)
public sealed record ServerboundMoveVehiclePacket(double X, double Y, double Z, float YRot, float XRot, bool OnGround) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundMoveVehiclePacket> StreamCodec { get; } = new MoveVehicleCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundMoveVehicle;

    public void Handle(ServerGamePacketListener handler) => handler.HandleMoveVehicle(this);

    private sealed class MoveVehicleCodec : StreamCodec<FriendlyByteBuf, ServerboundMoveVehiclePacket>
    {
        //While driving a vehicle the player reports its pose every tick: three double coordinates followed by two float angles and the on-ground flag
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
