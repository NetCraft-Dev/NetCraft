namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundMovePlayerPacket player movement packet, maps to vanilla ServerboundMovePlayerPacket
//Vanilla is an abstract base deriving four packet IDs Pos/PosRot/Rot/StatusOnly; here a single class carries all four variants
//HasPos/HasRot distinguish the variants, components not carried stay 0; flags is a single byte: bit0 on-ground, bit1 horizontal collision
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
    //PosStreamCodec maps to vanilla Pos: three double positions + flags
    public static StreamCodec<FriendlyByteBuf, ServerboundMovePlayerPacket> PosStreamCodec { get; } = new PosCodec();
    //PosRotStreamCodec maps to vanilla PosRot: three double positions + two float rotations + flags
    public static StreamCodec<FriendlyByteBuf, ServerboundMovePlayerPacket> PosRotStreamCodec { get; } = new PosRotCodec();
    //RotStreamCodec maps to vanilla Rot: two float rotations + flags
    public static StreamCodec<FriendlyByteBuf, ServerboundMovePlayerPacket> RotStreamCodec { get; } = new RotCodec();
    //StatusOnlyStreamCodec maps to vanilla StatusOnly: flags only
    public static StreamCodec<FriendlyByteBuf, ServerboundMovePlayerPacket> StatusOnlyStreamCodec { get; } = new StatusOnlyCodec();

    //Type restores each of the four vanilla subclass PacketTypes from HasPos/HasRot
    public PacketType<ServerGamePacketListener> Type =>
        HasPos
            ? (HasRot ? GamePacketTypes.ServerboundMovePlayerPosRot : GamePacketTypes.ServerboundMovePlayerPos)
            : (HasRot ? GamePacketTypes.ServerboundMovePlayerRot : GamePacketTypes.ServerboundMovePlayerStatusOnly);

    public void Handle(ServerGamePacketListener handler) => handler.HandleMovePlayer(this);

    //PackFlags vanilla packFlags, bit0 on-ground, bit1 horizontal collision
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
