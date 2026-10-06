using NetCraft.Primitives;

namespace NetCraft.Storage.Updates;

//BlockUpdateFlags, side-effect switches for setBlock, maps to vanilla Block.UPDATE_*
//Vanilla has no "strict mode early return"; setting a bit only makes the matching check miss, the method still runs to the end
public static class BlockUpdateFlags
{
    //Notify surrounding blocks after the change, triggering the neighbor update channel
    public const int Neighbours = 1;

    //Sync clients after the change
    public const int Clients = 2;

    //Suppress dirty marking on the client side
    public const int Invisible = 4;

    //Mark dirty immediately; 26.2 has no server-side reader, the constant is kept only so the zero value matches vanilla
    public const int Immediate = 8;

    //The caller declares the new shape known, skipping the shape update channel
    public const int KnownShape = 16;

    //Suppress drops, only takes effect in updateOrDestroy
    public const int SuppressDrops = 32;

    //Moved by piston, maps to the vanilla movedByPiston flag
    public const int MoveByPiston = 64;

    //Skip the whole shape update when the target is redstone wire
    public const int SkipShapeUpdateOnWire = 128;

    //Skip removal side effects of the old block entity
    public const int SkipBlockEntitySideEffects = 256;

    //Skip onPlace
    public const int SkipOnPlace = 512;

    //All side effects off, maps to vanilla 816, turning off the demotion, drops and known-shape checks at once
    public const int SkipAllSideEffects =
        SkipBlockEntitySideEffects | SkipOnPlace | SuppressDrops | KnownShape;

    //Does not notify clients and skips block entity side effects, maps to vanilla 260
    public const int None = SkipBlockEntitySideEffects | Invisible;

    //Neighbors plus clients, maps to vanilla 3, most common for ordinary placement
    public const int All = Neighbours | Clients;

    //UpdateLimitDefault, default chained propagation depth, the 512 filled in by the vanilla 3-arg overload
    public const int UpdateLimitDefault = 512;

    //NeighbourUpdateOrder, neighbor update traversal order, maps to vanilla NeighborUpdater.UPDATE_ORDER
    //The order itself is part of redstone timing, must not change
    public static readonly Direction[] NeighbourUpdateOrder =
    {
        Direction.West, Direction.East, Direction.Down, Direction.Up, Direction.North, Direction.South,
    };

    //ShapeUpdateOrder, shape update traversal order, maps to vanilla BlockBehaviour.UPDATE_SHAPE_ORDER
    //Different from the neighbor order, the two must not be mixed
    public static readonly Direction[] ShapeUpdateOrder =
    {
        Direction.West, Direction.East, Direction.North, Direction.South, Direction.Down, Direction.Up,
    };
}
