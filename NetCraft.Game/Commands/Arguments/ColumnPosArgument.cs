using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//ColumnCoordinates two horizontal coordinates, maps to the column coordinate parsed by vanilla ColumnPosArgument
//Difference from BlockCoordinates: only the two horizontal segments, no local coordinates
public sealed record ColumnCoordinates(WorldCoordinate X, WorldCoordinate Z)
{
    //GetColumn resolves the absolute block coordinate from the executor's position; the caller converts it to chunk coordinates
    public (int X, int Z) GetColumn(ServerCommandSource source)
    {
        var pos = source.Position;
        return ((int)Math.Floor(X.Get(pos.X)), (int)Math.Floor(Z.Get(pos.Z)));
    }
}

//ColumnPosArgument column coordinate argument, maps to vanilla net.minecraft.commands.arguments.coordinates.ColumnPosArgument
//Used by forceload to locate a chunk column; syntax: 0 0 or ~ ~1
public sealed class ColumnPosArgument : ArgumentType<ColumnCoordinates>
{
    public static readonly SimpleCommandExceptionType ErrorNotComplete =
        new(new TranslatableMessage("argument.pos2d.incomplete"));

    public static ColumnPosArgument ColumnPos() => new();

    public ColumnCoordinates Parse(StringReader reader)
    {
        var start = reader.Cursor;
        var x = WorldCoordinate.ParseInt(reader);
        if (!reader.CanRead() || reader.Peek() != ' ')
        {
            reader.SetCursor(start);
            throw ErrorNotComplete.CreateWithContext(reader);
        }
        reader.Skip();
        var z = WorldCoordinate.ParseInt(reader);
        return new ColumnCoordinates(x, z);
    }

    //GetColumn takes the parse result and resolves the absolute block coordinate from the executor's position
    public static (int X, int Z) GetColumn(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<ColumnCoordinates>(name).GetColumn((ServerCommandSource)context.GetSource());

    public IReadOnlyList<string> Examples => new[] { "0 0", "~ ~", "~1 ~-2" };
}
