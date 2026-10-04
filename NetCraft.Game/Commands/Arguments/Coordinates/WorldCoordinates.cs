using NetCraft.Commands;
using NetCraft.Game.Commands;
using NetCraft.Primitives;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//WorldCoordinates 世界坐标三段式对应原版 WorldCoordinates
//x/y/z 各段独立相对标志 空格分隔 缺段抛未完成错
public sealed record WorldCoordinates(WorldCoordinate X, WorldCoordinate Y, WorldCoordinate Z) : Coordinates
{
    public Vec3 GetPosition(ServerCommandSource source)
    {
        var pos = source.Position;
        return new Vec3(X.Get(pos.X), Y.Get(pos.Y), Z.Get(pos.Z));
    }

    //GetRotation 对齐原版 Vec2(x=pitch,y=yaw) x 段作用 pitch y 段作用 yaw
    public (float Yaw, float Pitch) GetRotation(ServerCommandSource source)
        => ((float)Y.Get(source.PlayerOrThrow.Yaw), (float)X.Get(source.PlayerOrThrow.Pitch));

    public bool IsXRelative => X.IsRelative;
    public bool IsYRelative => Y.IsRelative;
    public bool IsZRelative => Z.IsRelative;

    //ParseDouble 解析三段世界坐标 x/z 段应用居中修正 y 段不修正
    public static WorldCoordinates ParseDouble(StringReader reader, bool centerCorrect)
    {
        var start = reader.Cursor;
        var x = WorldCoordinate.ParseDouble(reader, centerCorrect);
        if (!reader.CanRead() || reader.Peek() != ' ')
        {
            reader.SetCursor(start);
            throw Vec3Argument.ErrorNotComplete.CreateWithContext(reader);
        }
        reader.Skip();
        var y = WorldCoordinate.ParseDouble(reader, false);
        if (!reader.CanRead() || reader.Peek() != ' ')
        {
            reader.SetCursor(start);
            throw Vec3Argument.ErrorNotComplete.CreateWithContext(reader);
        }
        reader.Skip();
        var z = WorldCoordinate.ParseDouble(reader, centerCorrect);
        return new WorldCoordinates(x, y, z);
    }

    //Absolute 构造绝对坐标
    public static WorldCoordinates Absolute(double x, double y, double z)
        => new(new WorldCoordinate(false, x), new WorldCoordinate(false, y), new WorldCoordinate(false, z));
}
