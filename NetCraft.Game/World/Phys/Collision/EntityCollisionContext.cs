using NetCraft.Game.World.Items;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Phys.Collision;

//EntityCollisionContext 带实体状态的碰撞上下文 对应原版 EntityCollisionContext
//实体底部高度决定方块要不要把它当"上方" 主手物品决定某些方块是否给碰撞
//Entity 用完全限定名 当前命名空间祖先里有同名的 NetCraft.Game.World.Entity 命名空间
public class EntityCollisionContext : CollisionContext
{
    //原版那个常数是 (double)(float)1.0E-5 照抄不要改成 1.0E-5
    private const double BelowTolerance = 9.999999747378752E-6;

    //EmptyWithoutFluidCollisions 无实体且不与流体碰撞的空上下文
    public static readonly CollisionContext EmptyWithoutFluidCollisions = new Empty(false);

    //EmptyWithFluidCollisions 无实体但与流体也碰撞的空上下文
    public static readonly CollisionContext EmptyWithFluidCollisions = new Empty(true);

    private readonly bool _descending;
    private readonly double _entityBottom;
    private readonly bool _placement;
    private readonly ItemStack _heldItem;
    private readonly bool _alwaysCollideWithFluid;
    private readonly NetCraft.Registry.Entity? _entity;

    public EntityCollisionContext(bool descending, bool placement, double entityBottom, ItemStack heldItem,
        bool alwaysCollideWithFluid, NetCraft.Registry.Entity? entity)
    {
        _descending = descending;
        _placement = placement;
        _entityBottom = entityBottom;
        _heldItem = heldItem;
        _alwaysCollideWithFluid = alwaysCollideWithFluid;
        _entity = entity;
    }

    //从实体身上取初始状态 对应原版那个待废弃构造器
    //原版这里会取 LivingEntity 的主手物品 物品体系接入前一律按空栈
    public EntityCollisionContext(NetCraft.Registry.Entity entity, bool alwaysCollideWithFluid, bool placement)
        : this(entity.IsDescending(), placement, entity.Pos.Y, ItemStack.Empty, alwaysCollideWithFluid, entity) { }

    public NetCraft.Registry.Entity? GetEntity() => _entity;

    public override bool IsDescending() => _descending;

    public override bool IsAbove(VoxelShape shape, BlockPos pos, bool defaultValue)
        => _entityBottom > pos.Y + shape.Max(Direction.Axis.Y) - BelowTolerance;

    public override bool IsHoldingItem(Item item) => !_heldItem.IsEmpty() && ReferenceEquals(_heldItem.GetItem(), item);

    public override bool AlwaysCollideWithFluid() => _alwaysCollideWithFluid;

    //原版要求实体能站在该流体上且上方流体不同种
    //液体承载体系（船 冰 岩浆行者一类）未接入 先一律返回否
    public override bool CanStandOnFluid(FluidState fluidStateAbove, FluidState fluid) => false;

    public override VoxelShape GetCollisionShape(BlockState state, CollisionGetter getter, BlockPos pos)
        => state.GetCollisionShape(getter, pos, this);

    public override bool IsPlacement => _placement;

    //Empty 无实体的空上下文 上方判定一律用调用方给的默认值
    private sealed class Empty : EntityCollisionContext
    {
        public Empty(bool alwaysCollideWithFluid)
            : base(false, false, -double.MaxValue, ItemStack.Empty, alwaysCollideWithFluid, null) { }

        public override bool IsAbove(VoxelShape shape, BlockPos pos, bool defaultValue) => defaultValue;
    }
}
