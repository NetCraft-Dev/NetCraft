using NetCraft.Game.World.Entity;
using NetCraft.Game.World.Items;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Phys.Collision;

//CollisionContext collision context, maps to vanilla net.minecraft.world.phys.shapes.CollisionContext
//The same block may give different collision shapes to different entities; scaffolding's descending relaxation and stairs' facing checks all ask it
public abstract class CollisionContext
{
    //IsDescending whether it is descending, maps to vanilla isDescending
    public abstract bool IsDescending();

    //IsAbove whether the entity is above the shape's top face; the out-of-bounds value is given by the caller, maps to vanilla isAbove
    public abstract bool IsAbove(VoxelShape shape, BlockPos pos, bool defaultValue);

    //IsHoldingItem whether the main hand holds the given item, maps to vanilla isHoldingItem
    public abstract bool IsHoldingItem(Item item);

    //AlwaysCollideWithFluid whether it also collides with fluids, maps to vanilla alwaysCollideWithFluid
    public abstract bool AlwaysCollideWithFluid();

    //CanStandOnFluid whether it can stand on the fluid, maps to vanilla canStandOnFluid
    public abstract bool CanStandOnFluid(FluidState fluidStateAbove, FluidState fluid);

    //GetCollisionShape gets the block's collision shape in the current context, maps to vanilla getCollisionShape
    public abstract VoxelShape GetCollisionShape(BlockState state, CollisionGetter getter, BlockPos pos);

    //IsPlacement whether it is a placement preview context, maps to vanilla isPlacement
    public virtual bool IsPlacement => false;

    //Empty empty context without an entity
    public static CollisionContext Empty => EntityCollisionContext.EmptyWithoutFluidCollisions;

    //EmptyWithFluidCollisions empty context without an entity that also collides with fluids
    public static CollisionContext EmptyWithFluidCollisions => EntityCollisionContext.EmptyWithFluidCollisions;

    //Of builds a context from an entity; vanilla switches to MinecartCollisionContext for experimental minecarts, but the minecart system is not wired up so the normal context is always used
    public static CollisionContext Of(NetCraft.Registry.Entity entity)
        => new EntityCollisionContext(entity, false, false);

    public static CollisionContext Of(NetCraft.Registry.Entity entity, bool alwaysCollideWithFluid)
        => new EntityCollisionContext(entity, alwaysCollideWithFluid, false);

    //PositionContext context carrying only the height coordinate; used to distinguish whether a block needs to face a horizontal collision
    public static CollisionContext PositionContext(double y) => new PositionCollisionContext(y);

    //PlacementContext placement preview context; before the item system is wired up the main hand item is always an empty stack
    public static CollisionContext PlacementContext(Player? player)
        => new EntityCollisionContext(
            player?.IsDescending() ?? false,
            true,
            player?.Pos.Y ?? -double.MaxValue,
            ItemStack.Empty,
            false,
            player);

    //WithPosition entity context with a given height; collisions before moving must use the position before entering this tick
    public static CollisionContext WithPosition(NetCraft.Registry.Entity? entity, double position)
        => new EntityCollisionContext(
            entity?.IsDescending() ?? false,
            true,
            entity is null ? -double.MaxValue : position,
            ItemStack.Empty,
            false,
            entity);
}
