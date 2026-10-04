using NetCraft.Game.World.Entity;
using NetCraft.Game.World.Items;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Phys.Collision;

//CollisionContext 碰撞上下文 对应原版 net.minecraft.world.phys.shapes.CollisionContext
//同一个方块对不同实体可能给出不同碰撞形状 脚手架的下落放宽 台阶的朝向判断都要问它
public abstract class CollisionContext
{
    //IsDescending 是否在下落 对应原版 isDescending
    public abstract bool IsDescending();

    //IsAbove 实体是否位于形状顶面之上 越界时的取值由调用方给 对应原版 isAbove
    public abstract bool IsAbove(VoxelShape shape, BlockPos pos, bool defaultValue);

    //IsHoldingItem 主手是否拿着指定物品 对应原版 isHoldingItem
    public abstract bool IsHoldingItem(Item item);

    //AlwaysCollideWithFluid 是否与流体也发生碰撞 对应原版 alwaysCollideWithFluid
    public abstract bool AlwaysCollideWithFluid();

    //CanStandOnFluid 能否站在该流体上 对应原版 canStandOnFluid
    public abstract bool CanStandOnFluid(FluidState fluidStateAbove, FluidState fluid);

    //GetCollisionShape 取方块在当前上下文下的碰撞形状 对应原版 getCollisionShape
    public abstract VoxelShape GetCollisionShape(BlockState state, CollisionGetter getter, BlockPos pos);

    //IsPlacement 是否为放置预览上下文 对应原版 isPlacement
    public virtual bool IsPlacement => false;

    //Empty 无实体的空上下文
    public static CollisionContext Empty => EntityCollisionContext.EmptyWithoutFluidCollisions;

    //EmptyWithFluidCollisions 无实体且与流体也发生碰撞的空上下文
    public static CollisionContext EmptyWithFluidCollisions => EntityCollisionContext.EmptyWithFluidCollisions;

    //Of 按实体建上下文 原版对实验性矿车会切 MinecartCollisionContext 矿车体系未接入一律走普通上下文
    public static CollisionContext Of(NetCraft.Registry.Entity entity)
        => new EntityCollisionContext(entity, false, false);

    public static CollisionContext Of(NetCraft.Registry.Entity entity, bool alwaysCollideWithFluid)
        => new EntityCollisionContext(entity, alwaysCollideWithFluid, false);

    //PositionContext 只带高度坐标的上下文 方块是否需要面对水平碰撞靠它区分
    public static CollisionContext PositionContext(double y) => new PositionCollisionContext(y);

    //PlacementContext 放置预览上下文 物品体系接入前主手物品一律按空栈
    public static CollisionContext PlacementContext(Player? player)
        => new EntityCollisionContext(
            player?.IsDescending() ?? false,
            true,
            player?.Pos.Y ?? -double.MaxValue,
            ItemStack.Empty,
            false,
            player);

    //WithPosition 带指定高度的实体上下文 移动前碰撞要用进入本刻前的位置
    public static CollisionContext WithPosition(NetCraft.Registry.Entity? entity, double position)
        => new EntityCollisionContext(
            entity?.IsDescending() ?? false,
            true,
            entity is null ? -double.MaxValue : position,
            ItemStack.Empty,
            false,
            entity);
}
