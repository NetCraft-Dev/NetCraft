using NetCraft.Primitives;
using StateDirection = NetCraft.Registry.Enums.Direction;

namespace NetCraft.Registry;

//DirectionConversions converts between the direction enum in block states and world coordinate directions
//Block properties store the enum form while world coordinate math uses Primitives.Direction, so the two types are converted back and forth
//Placed in the Registry root namespace to avoid name clashes when a block file uses both namespaces that contain Direction
public static class DirectionConversions
{
    //ToPrimitive converts the enum direction to a world direction
    public static Direction ToPrimitive(this StateDirection direction) => direction switch
    {
        StateDirection.down => Direction.Down,
        StateDirection.up => Direction.Up,
        StateDirection.north => Direction.North,
        StateDirection.south => Direction.South,
        StateDirection.west => Direction.West,
        _ => Direction.East,
    };

    //ToState converts a world direction to the enum direction
    public static StateDirection ToState(this Direction direction) => direction.Id3D switch
    {
        Direction.DownId => StateDirection.down,
        Direction.UpId => StateDirection.up,
        Direction.NorthId => StateDirection.north,
        Direction.SouthId => StateDirection.south,
        Direction.WestId => StateDirection.west,
        _ => StateDirection.east,
    };
}
