using NetCraft.Commands;
using NetCraft.Game.Commands;
using NetCraft.Primitives;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//LocalCoordinates three-segment local coordinates, maps to vanilla LocalCoordinates
//^left up forwards rotates into the world frame by the executor's facing; all three axes are relative
public sealed record LocalCoordinates(double Left, double Up, double Forwards) : Coordinates
{
    public Vec3 GetPosition(ServerCommandSource source)
    {
        var player = source.PlayerOrThrow;
        var offset = ApplyLocalCoordinatesToRotation(player.Yaw, player.Pitch, Left, Up, Forwards);
        var pos = player.Position;
        return new Vec3(offset.X + pos.X, offset.Y + pos.Y, offset.Z + pos.Z);
    }

    public (float Yaw, float Pitch) GetRotation(ServerCommandSource source) => (0f, 0f);

    public bool IsXRelative => true;
    public bool IsYRelative => true;
    public bool IsZRelative => true;

    //Parse parses the ^-prefixed three segments; whitespace after ^ means 0
    public static LocalCoordinates Parse(StringReader reader)
    {
        var start = reader.Cursor;
        var left = ReadDouble(reader, start);
        if (!reader.CanRead() || reader.Peek() != ' ')
        {
            reader.SetCursor(start);
            throw Vec3Argument.ErrorNotComplete.CreateWithContext(reader);
        }
        reader.Skip();
        var up = ReadDouble(reader, start);
        if (!reader.CanRead() || reader.Peek() != ' ')
        {
            reader.SetCursor(start);
            throw Vec3Argument.ErrorNotComplete.CreateWithContext(reader);
        }
        reader.Skip();
        var forwards = ReadDouble(reader, start);
        return new LocalCoordinates(left, up, forwards);
    }

    //ReadDouble a single segment must start with ^, otherwise roll back and throw a mixed-type error
    private static double ReadDouble(StringReader reader, int start)
    {
        if (!reader.CanRead())
            throw WorldCoordinate.ErrorExpectedDouble.CreateWithContext(reader);
        if (reader.Peek() != '^')
        {
            reader.SetCursor(start);
            throw Vec3Argument.ErrorMixedType.CreateWithContext(reader);
        }
        reader.Skip();
        if (!reader.CanRead() || reader.Peek() == ' ') return 0.0;
        return reader.ReadDouble();
    }

    //ApplyLocalCoordinatesToRotation builds the forward/up/left orthonormal basis from the facing and rotates the local offset into the world frame
    //maps to vanilla Vec3.applyLocalCoordinatesToRotation; the angle constant 0.017453292f converts degrees to radians
    public static Vec3 ApplyLocalCoordinatesToRotation(float yaw, float pitch, double left, double up, double forwards)
    {
        const float DegToRad = 0.017453292f;
        var yCos = MathF.Cos((yaw + 90.0f) * DegToRad);
        var ySin = MathF.Sin((yaw + 90.0f) * DegToRad);
        var xCos = MathF.Cos(-pitch * DegToRad);
        var xSin = MathF.Sin(-pitch * DegToRad);
        var xCosUp = MathF.Cos((-pitch + 90.0f) * DegToRad);
        var xSinUp = MathF.Sin((-pitch + 90.0f) * DegToRad);
        var fwd = new Vec3(yCos * xCos, xSin, ySin * xCos);
        var upVec = new Vec3(yCos * xCosUp, xSinUp, ySin * xCosUp);
        var leftVec = Cross(fwd, upVec).Multiply(-1.0);
        return new Vec3(
            fwd.X * forwards + upVec.X * up + leftVec.X * left,
            fwd.Y * forwards + upVec.Y * up + leftVec.Y * left,
            fwd.Z * forwards + upVec.Z * up + leftVec.Z * left);
    }

    private static Vec3 Cross(Vec3 a, Vec3 b)
        => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
}
