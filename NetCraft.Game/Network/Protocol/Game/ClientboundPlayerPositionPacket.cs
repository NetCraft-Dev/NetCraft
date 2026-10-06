using NetCraft.Network;

namespace NetCraft.Game.Network.Protocol.Game;

//RelativeFlag player position relative movement flag bit numbers, maps to vanilla Relative
//X/Y/Z position components relative, YRot/XRot rotation components relative, Delta three-axis velocity relative, RotateDelta velocity rotated by the rotation delta
public enum RelativeFlag
{
    X = 0,
    Y = 1,
    Z = 2,
    YRot = 3,
    XRot = 4,
    DeltaX = 5,
    DeltaY = 6,
    DeltaZ = 7,
    RotateDelta = 8,
}

//RelativeFlags relative flag set operations, maps to the static sets and bitmask codec of vanilla Relative
public static class RelativeFlags
{
    //Rotation rotation-only relative set; keeps the current rotation when a teleport does not explicitly provide one
    public static readonly IReadOnlySet<RelativeFlag> Rotation = new HashSet<RelativeFlag> { RelativeFlag.YRot, RelativeFlag.XRot };

    //Delta velocity-only relative set
    public static readonly IReadOnlySet<RelativeFlag> Delta = new HashSet<RelativeFlag> { RelativeFlag.DeltaX, RelativeFlag.DeltaY, RelativeFlag.DeltaZ, RelativeFlag.RotateDelta };

    //Direction velocity relative set; keeps the current velocity when coordinates are relative
    public static IReadOnlySet<RelativeFlag> Direction(bool x, bool y, bool z)
    {
        var set = new HashSet<RelativeFlag>();
        if (x) set.Add(RelativeFlag.DeltaX);
        if (y) set.Add(RelativeFlag.DeltaY);
        if (z) set.Add(RelativeFlag.DeltaZ);
        return set;
    }

    //Position position relative set; the client adds the packet values to the current coordinates
    public static IReadOnlySet<RelativeFlag> Position(bool x, bool y, bool z)
    {
        var set = new HashSet<RelativeFlag>();
        if (x) set.Add(RelativeFlag.X);
        if (y) set.Add(RelativeFlag.Y);
        if (z) set.Add(RelativeFlag.Z);
        return set;
    }

    //RotationOf rotation relative set
    public static IReadOnlySet<RelativeFlag> RotationOf(bool yRot, bool xRot)
    {
        var set = new HashSet<RelativeFlag>();
        if (yRot) set.Add(RelativeFlag.YRot);
        if (xRot) set.Add(RelativeFlag.XRot);
        return set;
    }

    //Union merges several sets
    public static IReadOnlySet<RelativeFlag> Union(params IReadOnlySet<RelativeFlag>[] sets)
    {
        var result = new HashSet<RelativeFlag>();
        foreach (var set in sets) result.UnionWith(set);
        return result;
    }

    //Pack converts a set to a bitmask
    public static int Pack(IEnumerable<RelativeFlag> set)
    {
        var result = 0;
        foreach (var flag in set) result |= 1 << (int)flag;
        return result;
    }

    //Unpack restores a set from a bitmask
    public static IReadOnlyList<RelativeFlag> Unpack(int value)
    {
        var result = new List<RelativeFlag>();
        foreach (var flag in Enum.GetValues<RelativeFlag>())
            if ((value & (1 << (int)flag)) != 0) result.Add(flag);
        return result;
    }
}

//ClientboundPlayerPositionPacket player position sync packet, maps to vanilla ClientboundPlayerPositionPacket
//S4 26.2 refactored to the PositionMoveRotation format
//Fields: Id(VarInt), X/Y/Z (Double position), DX/DY/DZ (Double deltaMovement), YRot/XRot (Float), Relatives (Int bitmask)
public sealed record ClientboundPlayerPositionPacket(
    double X,
    double Y,
    double Z,
    float YRot,
    float XRot,
    int Relatives,
    int Id) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundPlayerPositionPacket> StreamCodec { get; } = new PlayerPositionCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundPlayerPosition;

    public void Handle(ClientGamePacketListener handler) => handler.HandleMovePlayer(this);

    private sealed class PlayerPositionCodec : StreamCodec<FriendlyByteBuf, ClientboundPlayerPositionPacket>
    {
        public ClientboundPlayerPositionPacket Decode(FriendlyByteBuf buf)
        {
            //26.2: id varint + position(3 double) + deltaMovement(3 double) + yRot/xRot float + relatives int
            var id = buf.ReadVarInt();
            var x = buf.ReadDouble();
            var y = buf.ReadDouble();
            var z = buf.ReadDouble();
            buf.ReadDouble();
            buf.ReadDouble();
            buf.ReadDouble();
            var yRot = buf.ReadFloat();
            var xRot = buf.ReadFloat();
            var relatives = buf.ReadInt();
            return new ClientboundPlayerPositionPacket(x, y, z, yRot, xRot, relatives, id);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundPlayerPositionPacket value)
        {
            buf.WriteVarInt(value.Id);
            buf.WriteDouble(value.X);
            buf.WriteDouble(value.Y);
            buf.WriteDouble(value.Z);
            //deltaMovement defaults to 0, aligning with PositionMoveRotation.deltaMovement
            buf.WriteDouble(0);
            buf.WriteDouble(0);
            buf.WriteDouble(0);
            buf.WriteFloat(value.YRot);
            buf.WriteFloat(value.XRot);
            //Relative.SET_STREAM_CODEC int bitmask
            buf.WriteInt(value.Relatives);
        }
    }
}
