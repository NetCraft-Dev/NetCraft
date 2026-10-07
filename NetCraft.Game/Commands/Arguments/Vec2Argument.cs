using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Game.Commands;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//Vec2Argument 2D coordinate argument, maps to vanilla Vec2Argument
//Parses the x z world coordinate segments; a missing segment throws an incomplete error; on evaluation the y segment is filled from the executor's position
public sealed class Vec2Argument(bool centerCorrect) : ArgumentType<Coordinates>
{
    public static readonly SimpleCommandExceptionType ErrorNotComplete =
        new(new TranslatableMessage("argument.pos2d.incomplete"));

    public static Vec2Argument Vec2() => new(true);

    public static Vec2Argument Vec2(bool centerCorrect) => new(centerCorrect);

    public Coordinates Parse(StringReader reader)
    {
        var start = reader.Cursor;
        if (!reader.CanRead()) throw ErrorNotComplete.CreateWithContext(reader);
        var x = WorldCoordinate.ParseDouble(reader, centerCorrect);
        if (!reader.CanRead() || reader.Peek() != ' ')
        {
            reader.SetCursor(start);
            throw ErrorNotComplete.CreateWithContext(reader);
        }
        reader.Skip();
        var z = WorldCoordinate.ParseDouble(reader, centerCorrect);
        //The y segment is filled as relative 0; the 2D argument has no height
        return new WorldCoordinates(x, new WorldCoordinate(true, 0.0), z);
    }

    //GetCoordinates gets the parse result
    public static Coordinates GetCoordinates(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<Coordinates>(name);

    //GetVec2 gets the x/z components of the absolute coordinate, maps to vanilla getVec2
    public static (double X, double Z) GetVec2(CommandContext<CommandSourceStack> context, string name)
    {
        var position = GetCoordinates(context, name).GetPosition((ServerCommandSource)context.GetSource());
        return (position.X, position.Z);
    }

    public IReadOnlyList<string> Examples => new[] { "0 0", "~ ~", "0.1 -0.5", "~1 ~-2" };
}
