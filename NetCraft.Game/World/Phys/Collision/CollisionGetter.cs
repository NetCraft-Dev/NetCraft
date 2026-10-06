using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry.State;
using NetCraft.Storage;

namespace NetCraft.Game.World.Phys.Collision;

//CollisionGetter world view that can perform collision queries, maps to vanilla net.minecraft.world.level.CollisionGetter
//The minimal set keeps only the entry points shape queries need
//Unwired parts: world border checks are always true, entity collisions are always empty, suffocation block iteration and free-spot search are left to the entity movement stage
//The source parameter uniformly uses the fully qualified name; an ancestor namespace here has the same-named NetCraft.Game.World.Entity namespace
public interface CollisionGetter : BlockGetter
{
    //GetChunkForCollisions gets the block view of a chunk for collision queries; a fully readable world returns itself
    BlockGetter GetChunkForCollisions(int chunkX, int chunkZ) => this;

    //GetEntityCollisions entity hit boxes intersecting the test area; the entity collision system is not wired up so it is always empty
    IReadOnlyList<VoxelShape> GetEntityCollisions(NetCraft.Registry.Entity? source, AABB testArea)
        => Array.Empty<VoxelShape>();

    //IsUnobstructed whether placing a shape into this world is not blocked; before the entity collision system is wired up only blocks are checked
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

    //NoCollision only when blocks, entities and the world border all do not block, maps to vanilla noCollision
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

    //The world border system is not wired up, so always true for now; once wired it must switch to world border collision shape checks
    bool NoBorderCollision(NetCraft.Registry.Entity? source, AABB aabb) => true;

    //GetCollisions yields entity and block collision shapes together, maps to vanilla getCollisions
    IEnumerable<VoxelShape> GetCollisions(NetCraft.Registry.Entity? source, AABB box)
    {
        var entityCollisions = GetEntityCollisions(source, box);
        var blockCollisions = GetBlockCollisions(source, box);
        return entityCollisions.Count == 0 ? blockCollisions : entityCollisions.Concat(blockCollisions);
    }

    //GetPreMoveCollisions queries block collisions at the position before the entity entered this tick, maps to vanilla getPreMoveCollisions
    //Used to distinguish whether the entity still counts as standing on top just after crossing a block cell
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
