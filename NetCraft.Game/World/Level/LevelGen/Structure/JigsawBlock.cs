using NetCraft.Registry.Enums;
using NetCraft.Registry.State;
using GameDirection = NetCraft.Primitives.Direction;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//JigsawBlock jigsaw block semantics helper, maps to vanilla net.minecraft.world.level.block.JigsawBlock
//The jigsaw block is not registered yet; only the static semantics needed for connection checks are extracted here, to be moved into the block class once it exists
public static class JigsawBlock
{
    //OrientationPropertyName orientation property name, maps to vanilla JigsawBlock.ORIENTATION
    public const string OrientationPropertyName = "orientation";

    //GetFrontFacing returns the front facing, maps to vanilla getFrontFacing
    //Falls back to the block default facing north when the orientation property is missing or invalid
    public static GameDirection GetFrontFacing(BlockState state)
        => GetOrientation(state) is { } orientation ? FrontOf(orientation) : GameDirection.North;

    //GetTopFacing returns the top facing, maps to vanilla getTopFacing
    public static GameDirection GetTopFacing(BlockState state)
        => GetOrientation(state) is { } orientation ? TopOf(orientation) : GameDirection.Up;

    //CanAttach whether two jigsaw blocks can attach, maps to vanilla canAttach
    //Three conditions must all hold: the source front is the target front's opposite, the joint is rollable or the top facings match, and the source target equals the target name
    public static bool CanAttach(StructureTemplate.JigsawBlockInfo source, StructureTemplate.JigsawBlockInfo target)
    {
        var sourceFront = GetFrontFacing(source.Info.State);
        var targetFront = GetFrontFacing(target.Info.State);
        var sourceTop = GetTopFacing(source.Info.State);
        var targetTop = GetTopFacing(target.Info.State);
        var rollable = source.JointType == JointType.Rollable;
        return sourceFront == targetFront.Opposite
            && (rollable || sourceTop == targetTop)
            && source.Target == target.Name;
    }

    //GetOrientation reads the orientation property value, returns null when the property is absent
    private static FrontAndTop? GetOrientation(BlockState state)
    {
        foreach (var entry in state.GetValues())
        {
            if (entry.Property.Name != OrientationPropertyName || entry.Value is not FrontAndTop orientation) continue;
            return orientation;
        }
        return null;
    }

    //FrontOf returns the front of an orientation pair, maps to vanilla FrontAndTop.getFront; the first half of the enum name
    public static GameDirection FrontOf(FrontAndTop orientation) => orientation switch
    {
        FrontAndTop.down_east or FrontAndTop.down_north or FrontAndTop.down_south or FrontAndTop.down_west
            => GameDirection.Down,
        FrontAndTop.up_east or FrontAndTop.up_north or FrontAndTop.up_south or FrontAndTop.up_west
            => GameDirection.Up,
        FrontAndTop.west_up => GameDirection.West,
        FrontAndTop.east_up => GameDirection.East,
        FrontAndTop.north_up => GameDirection.North,
        _ => GameDirection.South,
    };

    //TopOf returns the top of an orientation pair, maps to vanilla FrontAndTop.getTop; the second half of the enum name
    public static GameDirection TopOf(FrontAndTop orientation) => orientation switch
    {
        FrontAndTop.west_up or FrontAndTop.east_up or FrontAndTop.north_up or FrontAndTop.south_up
            => GameDirection.Up,
        FrontAndTop.down_east or FrontAndTop.up_east => GameDirection.East,
        FrontAndTop.down_north or FrontAndTop.up_north => GameDirection.North,
        FrontAndTop.down_south or FrontAndTop.up_south => GameDirection.South,
        _ => GameDirection.West,
    };

    //FromFrontAndTop looks up the orientation pair from front and top, maps to vanilla FrontAndTop.fromFrontAndTop
    public static FrontAndTop FromFrontAndTop(GameDirection front, GameDirection top)
    {
        foreach (var candidate in Enum.GetValues<FrontAndTop>())
            if (FrontOf(candidate) == front && TopOf(candidate) == top) return candidate;
        throw new ArgumentException($"no orientation pair with front {front} and top {top}");
    }
}
