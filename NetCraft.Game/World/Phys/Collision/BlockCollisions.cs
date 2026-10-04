using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Storage;

namespace NetCraft.Game.World.Phys.Collision;

//BlockCollisions 遍历测试区覆盖到的方块 产出与其相交的碰撞形状 对应原版 BlockCollisions
//原版用 Cursor3D 游标并借边界标记跳过不可能相交的方块 这里按等价的三重循环遍历 结果一致只是少了那层剪枝
public sealed class BlockCollisions<T> : IEnumerable<T>
{
    //原版盒体边界朝外留的容差 边界刚好压线的方块也要算进来
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
        //范围比测试区各向外扩一格 形状可能从相邻格伸进来
        var x0 = Floor(_box.Min.X - Tolerance) - 1;
        var x1 = Floor(_box.Max.X + Tolerance) + 1;
        var y0 = Floor(_box.Min.Y - Tolerance) - 1;
        var y1 = Floor(_box.Max.Y + Tolerance) + 1;
        var z0 = Floor(_box.Min.Z - Tolerance) - 1;
        var z1 = Floor(_box.Max.Z + Tolerance) + 1;

        //同一区块内的方块共用一个视图 跨区块才重新取
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
            //整块的形状不必构造平移副本 直接拿格子与测试区比更快
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
