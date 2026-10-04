using NetCraft.Commands;
using NetCraft.Game.Commands;
using NetCraft.Primitives;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//LocalCoordinates 本地坐标三段式对应原版 LocalCoordinates
//^left up forwards 按执行者朝向旋转到世界系 三轴恒相对
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

    //Parse 解析 ^ 前缀三段 ^ 后空白视为 0
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

    //ReadDouble 单段必须 ^ 开头否则回滚抛混合类型错
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

    //ApplyLocalCoordinatesToRotation 按朝向构造前进/上方/左侧正交基把本地偏移旋转到世界系
    //对应原版 Vec3.applyLocalCoordinatesToRotation 角度换算 0.017453292f 为度转弧度
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
