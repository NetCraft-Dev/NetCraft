using NetCraft.Commands;
using NetCraft.Game.Commands;
using NetCraft.Primitives;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//WorldCoordinates three-segment world coordinates, maps to vanilla WorldCoordinates
//x/y/z each have an independent relative flag, separated by spaces; a missing segment throws an incomplete error
public sealed record WorldCoordinates(WorldCoordinate X, WorldCoordinate Y, WorldCoordinate Z) : Coordinates
{
    public Vec3 GetPosition(ServerCommandSource source)
    {
        var pos = source.Position;
        return new Vec3(X.Get(pos.X), Y.Get(pos.Y), Z.Get(pos.Z));
    }

    //GetRotation aligned with vanilla Vec2(x=pitch,y=yaw); the x segment drives pitch and the y segment drives yaw
    public (float Yaw, float Pitch) GetRotation(ServerCommandSource source)
        => ((float)Y.Get(source.PlayerOrThrow.Yaw), (float)X.Get(source.PlayerOrThrow.Pitch));

    public bool IsXRelative => X.IsRelative;
    public bool IsYRelative => Y.IsRelative;
    public bool IsZRelative => Z.IsRelative;

    //ParseDouble parses the three world coordinate segments; x/z apply center correction, y does not
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

    //Absolute builds absolute coordinates
    public static WorldCoordinates Absolute(double x, double y, double z)
        => new(new WorldCoordinate(false, x), new WorldCoordinate(false, y), new WorldCoordinate(false, z));
}
