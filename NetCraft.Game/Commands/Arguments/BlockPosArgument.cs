using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Primitives;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//BlockCoordinates three-segment block coordinates, maps to the coordinate triple parsed by vanilla BlockPosArgument
//Difference from WorldCoordinates: no ^ local coordinates, and absolute segments accept integers only
public sealed record BlockCoordinates(WorldCoordinate X, WorldCoordinate Y, WorldCoordinate Z)
{
    //GetBlockPos resolves the absolute coordinate from the executor's position; relative segments allow decimals so always floor to a block cell
    public BlockPos GetBlockPos(ServerCommandSource source)
    {
        var pos = source.Position;
        return new BlockPos(
            (int)Math.Floor(X.Get(pos.X)),
            (int)Math.Floor(Y.Get(pos.Y)),
            (int)Math.Floor(Z.Get(pos.Z)));
    }
}

//BlockPosArgument block coordinate argument, maps to vanilla net.minecraft.commands.arguments.coordinates.BlockPosArgument
//Used by setblock and fill to locate; syntax: 0 0 0 or ~ ~1 ~
public sealed class BlockPosArgument : ArgumentType<BlockCoordinates>
{
    public static readonly SimpleCommandExceptionType ErrorNotComplete =
        new(new TranslatableMessage("argument.pos3d.incomplete"));

    public static BlockPosArgument BlockPos() => new();

    public BlockCoordinates Parse(StringReader reader)
    {
        var start = reader.Cursor;
        var x = WorldCoordinate.ParseInt(reader);
        if (!reader.CanRead() || reader.Peek() != ' ')
        {
            reader.SetCursor(start);
            throw ErrorNotComplete.CreateWithContext(reader);
        }
        reader.Skip();
        var y = WorldCoordinate.ParseInt(reader);
        if (!reader.CanRead() || reader.Peek() != ' ')
        {
            reader.SetCursor(start);
            throw ErrorNotComplete.CreateWithContext(reader);
        }
        reader.Skip();
        var z = WorldCoordinate.ParseInt(reader);
        return new BlockCoordinates(x, y, z);
    }

    //GetBlockPos takes the parse result and resolves the absolute coordinate from the executor's position
    public static BlockPos GetBlockPos(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<BlockCoordinates>(name).GetBlockPos((ServerCommandSource)context.GetSource());

    public IReadOnlyList<string> Examples => new[] { "0 0 0", "~ ~ ~", "~1 ~-2 ~3" };
}
