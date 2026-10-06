using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Storage;

namespace NetCraft.Game.World.Phys.Collision;

//BlockCollisions iterates the blocks covered by the test area and yields the collision shapes intersecting it, maps to vanilla BlockCollisions
//Vanilla uses a Cursor3D cursor and boundary flags to skip blocks that cannot intersect; this uses an equivalent triple loop with the same result, only missing that pruning layer
public sealed class BlockCollisions<T> : IEnumerable<T>
{
    //The tolerance vanilla leaves outward on the box bounds; blocks whose bounds just touch the line are also counted
    private const double Tolerance = 1.0E-7;

    private readonly CollisionGetter _collisionGetter;
    private readonly CollisionContext _context;
    private readonly AABB _box;
    private readonly VoxelShape _entityShape;
    private readonly Func<BlockPos, VoxelShape, T> _resultProvider;

    public BlockCollisions(CollisionGetter collisionGetter, NetCraft.Registry.Entity? source, AABB box,
        Func<BlockPos, VoxelShape, T> resultProvider)
        : this(collisionGetter, source is null ? CollisionContext.Empty : CollisionContext.Of(source), box,
            resultProvider) { }

    public BlockCollisions(CollisionGetter collisionGetter, CollisionContext context, AABB box,
        Func<BlockPos, VoxelShape, T> resultProvider)
    {
        _collisionGetter = collisionGetter;
        _context = context;
        _box = box;
        _entityShape = Shapes.Create(box);
        _resultProvider = resultProvider;
    }

    public IEnumerator<T> GetEnumerator()
    {
        //The range is expanded one block outward on each side of the test area; shapes may extend in from a neighboring cell
        var x0 = Floor(_box.Min.X - Tolerance) - 1;
        var x1 = Floor(_box.Max.X + Tolerance) + 1;
        var y0 = Floor(_box.Min.Y - Tolerance) - 1;
        var y1 = Floor(_box.Max.Y + Tolerance) + 1;
        var z0 = Floor(_box.Min.Z - Tolerance) - 1;
        var z1 = Floor(_box.Max.Z + Tolerance) + 1;

        //Blocks within the same chunk share one view; it is re-fetched only across chunks
        BlockGetter? chunk = null;
        var cachedChunkX = int.MinValue;
        var cachedChunkZ = int.MinValue;

        for (var x = x0; x <= x1; x++)
        for (var y = y0; y <= y1; y++)
        for (var z = z0; z <= z1; z++)
        {
            var chunkX = SectionPos.BlockToSectionCoord(x);
            var chunkZ = SectionPos.BlockToSectionCoord(z);
            if (chunk is null || chunkX != cachedChunkX || chunkZ != cachedChunkZ)
            {
                chunk = _collisionGetter.GetChunkForCollisions(chunkX, chunkZ);
                cachedChunkX = chunkX;
                cachedChunkZ = chunkZ;
            }

            var pos = new BlockPos(x, y, z);
            var state = chunk.GetBlockState(x, y, z);
            var shape = _context.GetCollisionShape(state, _collisionGetter, pos);
            //A full block's shape needs no translated copy; comparing the cell with the test area directly is faster
            if (ReferenceEquals(shape, Shapes.Block()))
            {
                if (_box.Intersects(x, y, z, x + 1.0, y + 1.0, z + 1.0))
                    yield return _resultProvider(pos, shape.Move(x, y, z));
                continue;
            }

            var moved = shape.Move(x, y, z);
            if (!moved.IsEmpty && Shapes.JoinIsNotEmpty(moved, _entityShape, BooleanOps.And))
                yield return _resultProvider(pos, moved);
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    private static int Floor(double value) => (int)Math.Floor(value);
}
