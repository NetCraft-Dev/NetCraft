namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundMovePlayerPacket 玩家移动包对应原版 ServerboundMovePlayerPacket
//原版是抽象基类派生出 Pos/PosRot/Rot/StatusOnly 四个包 ID 这里用单类承载四个变体
//由 HasPos/HasRot 区分变体 未携带的分量保持 0 flags 单字节: 位0 着地 位1 水平碰撞
public sealed record ServerboundMovePlayerPacket(
    double X,
    double Y,
    double Z,
    float YRot,
    float XRot,
    bool OnGround,
    bool HorizontalCollision,
    bool HasPos,
    bool HasRot) : Packet<ServerGamePacketListener>
{
    //PosStreamCodec 对应原版 Pos 三 double 位置 + flags
    public static StreamCodec<FriendlyByteBuf, ServerboundMovePlayerPacket> PosStreamCodec { get; } = new PosCodec();
    //PosRotStreamCodec 对应原版 PosRot 三 double 位置 + 两 float 朝向 + flags
    public static StreamCodec<FriendlyByteBuf, ServerboundMovePlayerPacket> PosRotStreamCodec { get; } = new PosRotCodec();
    //RotStreamCodec 对应原版 Rot 两 float 朝向 + flags
    public static StreamCodec<FriendlyByteBuf, ServerboundMovePlayerPacket> RotStreamCodec { get; } = new RotCodec();
    //StatusOnlyStreamCodec 对应原版 StatusOnly 仅 flags
    public static StreamCodec<FriendlyByteBuf, ServerboundMovePlayerPacket> StatusOnlyStreamCodec { get; } = new StatusOnlyCodec();

    //Type 按 HasPos/HasRot 还原原版四个子类各自的 PacketType
    public PacketType<ServerGamePacketListener> Type =>
        HasPos
            ? (HasRot ? GamePacketTypes.ServerboundMovePlayerPosRot : GamePacketTypes.ServerboundMovePlayerPos)
            : (HasRot ? GamePacketTypes.ServerboundMovePlayerRot : GamePacketTypes.ServerboundMovePlayerStatusOnly);

    public void Handle(ServerGamePacketListener handler) => handler.HandleMovePlayer(this);

    //PackFlags 原版 packFlags 位0 着地 位1 水平碰撞
    private static byte PackFlags(bool onGround, bool horizontalCollision)
        => (byte)((onGround ? 1 : 0) | (horizontalCollision ? 2 : 0));

    private sealed class PosCodec : StreamCodec<FriendlyByteBuf, ServerboundMovePlayerPacket>
    {
        public ServerboundMovePlayerPacket Decode(FriendlyByteBuf buf)
        {
            var x = buf.ReadDouble();
            var y = buf.ReadDouble();
            var z = buf.ReadDouble();
            var flags = buf.ReadByte();
            return new(x, y, z, 0f, 0f, (flags & 1) != 0, (flags & 2) != 0, true, false);
        }

        public void Encode(FriendlyByteBuf buf, ServerboundMovePlayerPacket value)
        {
            buf.WriteDouble(value.X);
            buf.WriteDouble(value.Y);
            buf.WriteDouble(value.Z);
            buf.WriteByte(PackFlags(value.OnGround, value.HorizontalCollision));
        }
    }

    private sealed class PosRotCodec : StreamCodec<FriendlyByteBuf, ServerboundMovePlayerPacket>
    {
        public ServerboundMovePlayerPacket Decode(FriendlyByteBuf buf)
        {
            var x = buf.ReadDouble();
            var y = buf.ReadDouble();
            var z = buf.ReadDouble();
            var yRot = buf.ReadFloat();
            var xRot = buf.ReadFloat();
            var flags = buf.ReadByte();
            return new(x, y, z, yRot, xRot, (flags & 1) != 0, (flags & 2) != 0, true, true);
        }

        public void Encode(FriendlyByteBuf buf, ServerboundMovePlayerPacket value)
        {
            buf.WriteDouble(value.X);
            buf.WriteDouble(value.Y);
            buf.WriteDouble(value.Z);
            buf.WriteFloat(value.YRot);
            buf.WriteFloat(value.XRot);
            buf.WriteByte(PackFlags(value.OnGround, value.HorizontalCollision));
        }
    }

    private sealed class RotCodec : StreamCodec<FriendlyByteBuf, ServerboundMovePlayerPacket>
    {
        public ServerboundMovePlayerPacket Decode(FriendlyByteBuf buf)
        {
            var yRot = buf.ReadFloat();
            var xRot = buf.ReadFloat();
            var flags = buf.ReadByte();
            return new(0, 0, 0, yRot, xRot, (flags & 1) != 0, (flags & 2) != 0, false, true);
        }

        public void Encode(FriendlyByteBuf buf, ServerboundMovePlayerPacket value)
        {
            buf.WriteFloat(value.YRot);
            buf.WriteFloat(value.XRot);
            buf.WriteByte(PackFlags(value.OnGround, value.HorizontalCollision));
        }
    }

    private sealed class StatusOnlyCodec : StreamCodec<FriendlyByteBuf, ServerboundMovePlayerPacket>
    {
        public ServerboundMovePlayerPacket Decode(FriendlyByteBuf buf)
        {
            var flags = buf.ReadByte();
            return new(0, 0, 0, 0f, 0f, (flags & 1) != 0, (flags & 2) != 0, false, false);
        }

        public void Encode(FriendlyByteBuf buf, ServerboundMovePlayerPacket value)
            => buf.WriteByte(PackFlags(value.OnGround, value.HorizontalCollision));
    }
}
