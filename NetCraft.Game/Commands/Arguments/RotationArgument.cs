using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//RotationArgument rotation argument, maps to vanilla RotationArgument
//Two segments yaw pitch, each supporting ~ relative; the z segment is fixed relative 0
public sealed class RotationArgument : ArgumentType<Coordinates>
{
    public static readonly SimpleCommandExceptionType ErrorNotComplete =
        new(new TranslatableMessage("argument.rotation.incomplete"));

    public static RotationArgument Rotation() => new();

    public Coordinates Parse(StringReader reader)
    {
        var start = reader.Cursor;
        if (!reader.CanRead())
            throw ErrorNotComplete.CreateWithContext(reader);
        //The first input segment drives yaw, the second pitch; at construction the x segment holds pitch and the y segment holds yaw, aligned with vanilla Vec2(x=pitch,y=yaw)
        var yaw = WorldCoordinate.ParseDouble(reader, false);
        if (!reader.CanRead() || reader.Peek() != ' ')
        {
            reader.SetCursor(start);
            throw ErrorNotComplete.CreateWithContext(reader);
        }
        reader.Skip();
        var pitch = WorldCoordinate.ParseDouble(reader, false);
        return new WorldCoordinates(pitch, yaw, new WorldCoordinate(true, 0.0));
    }

    //GetRotation gets the parse result
    public static Coordinates GetRotation(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<Coordinates>(name);

    public IReadOnlyList<string> Examples => new[] { "0 0", "~ ~", "~-5 ~5" };
}
