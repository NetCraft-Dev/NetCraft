using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry.State;
using NetCraft.Storage;

namespace NetCraft.Game.World.Phys.Collision;

//CollisionGetter 能做碰撞查询的世界视图 对应原版 net.minecraft.world.level.CollisionGetter
//最小集只保留形状查询需要的入口
//未接入的部分 世界边界判定恒真 实体碰撞一律空 窒息方块遍历与自由落点搜索留到实体移动阶段
//source 参数统一用完全限定名 当前命名空间祖先里有同名的 NetCraft.Game.World.Entity 命名空间
public interface CollisionGetter : BlockGetter
{
    //GetChunkForCollisions 取某区块用于碰撞查询的方块视图 整块可读的世界直接返回自身
    BlockGetter GetChunkForCollisions(int chunkX, int chunkZ) => this;

    //GetEntityCollisions 与测试区相交的实体碰撞盒 实体碰撞体系未接入一律空
    IReadOnlyList<VoxelShape> GetEntityCollisions(NetCraft.Registry.Entity? source, AABB testArea)
        => Array.Empty<VoxelShape>();

    //IsUnobstructed 形状放进这个世界是否不被挡住 实体碰撞体系接入前只看方块
    bool IsUnobstructed(NetCraft.Registry.Entity? source, VoxelShape shape) => true;

    bool IsUnobstructed(BlockState state, BlockPos pos, CollisionContext context)
    {
        var shape = state.GetCollisionShape(this, pos, context);
        return shape.IsEmpty || IsUnobstructed(null, shape.Move(pos.X, pos.Y, pos.Z));
    }

    bool IsUnobstructed(NetCraft.Registry.Entity? source)
        => source is null || IsUnobstructed(source, Shapes.Create(source.BoundingBox));

    bool NoCollision(AABB aabb) => NoCollision(null, aabb);

    bool NoCollision(NetCraft.Registry.Entity? source, AABB aabb) => NoCollision(source, aabb, false);

    //NoCollision 方块 实体 世界边界三样都不挡才算无碰撞 对应原版 noCollision
    bool NoCollision(NetCraft.Registry.Entity? source, AABB aabb, bool alwaysCollideWithFluids)
        => NoBlockCollision(source, aabb, alwaysCollideWithFluids)
            && NoEntityCollision(source, aabb)
            && NoBorderCollision(source, aabb);

    bool NoBlockCollision(NetCraft.Registry.Entity? source, AABB aabb) => NoBlockCollision(source, aabb, false);

    bool NoBlockCollision(NetCraft.Registry.Entity? source, AABB aabb, bool alwaysCollideWithFluids)
    {
        var collisions = alwaysCollideWithFluids
            ? GetBlockAndLiquidCollisions(source, aabb)
            : GetBlockCollisions(source, aabb);
        foreach (var shape in collisions)
            if (!shape.IsEmpty) return false;
        return true;
    }

    bool NoEntityCollision(NetCraft.Registry.Entity? source, AABB aabb)
        => GetEntityCollisions(source, aabb).Count == 0;

    //世界边界体系未接入 先恒真 接入后要换成世界边界的碰撞形状判定
    bool NoBorderCollision(NetCraft.Registry.Entity? source, AABB aabb) => true;

    //GetCollisions 实体与方块的碰撞形状一起给 对应原版 getCollisions
    IEnumerable<VoxelShape> GetCollisions(NetCraft.Registry.Entity? source, AABB box)
    {
        var entityCollisions = GetEntityCollisions(source, box);
        var blockCollisions = GetBlockCollisions(source, box);
        return entityCollisions.Count == 0 ? blockCollisions : entityCollisions.Concat(blockCollisions);
    }

    //GetPreMoveCollisions 按实体进入本刻前的位置查方块碰撞 对应原版 getPreMoveCollisions
    //实体刚跨过方块格时是否还算站在上面 靠它区分
    IEnumerable<VoxelShape> GetPreMoveCollisions(NetCraft.Registry.Entity source, AABB box, Vec3 oldPos)
    {
        var entityCollisions = GetEntityCollisions(source, box);
        var blockCollisions = GetBlockCollisionsFromContext(CollisionContext.WithPosition(source, oldPos.Y), box);
        return entityCollisions.Count == 0 ? blockCollisions : entityCollisions.Concat(blockCollisions);
    }

    IEnumerable<VoxelShape> GetBlockCollisions(NetCraft.Registry.Entity? source, AABB box)
        => GetBlockCollisionsFromContext(source is null ? CollisionContext.Empty : CollisionContext.Of(source), box);

    IEnumerable<VoxelShape> GetBlockAndLiquidCollisions(NetCraft.Registry.Entity? source, AABB box)
        => GetBlockCollisionsFromContext(
            source is null ? CollisionContext.EmptyWithFluidCollisions : CollisionContext.Of(source, true), box);

    IEnumerable<VoxelShape> GetBlockCollisionsFromContext(CollisionContext source, AABB box)
        => new BlockCollisions<VoxelShape>(this, source, box, (_, shape) => shape);
}
