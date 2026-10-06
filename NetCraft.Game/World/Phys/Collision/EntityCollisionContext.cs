using NetCraft.Game.World.Items;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Phys.Collision;

//EntityCollisionContext collision context carrying entity state, maps to vanilla EntityCollisionContext
//The entity's bottom height decides whether a block treats it as "above"; the main hand item decides whether some blocks give collision
//Entity uses the fully qualified name; an ancestor namespace here has the same-named NetCraft.Game.World.Entity namespace
public class EntityCollisionContext : CollisionContext
{
    //Vanilla's constant is (double)(float)1.0E-5; copy it as is and do not change it to 1.0E-5
    private const double BelowTolerance = 9.999999747378752E-6;

    //EmptyWithoutFluidCollisions empty context without an entity that does not collide with fluids
    public static readonly CollisionContext EmptyWithoutFluidCollisions = new Empty(false);

    //EmptyWithFluidCollisions empty context without an entity that also collides with fluids
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

    //Takes the initial state from the entity, maps to the vanilla deprecated constructor
    //Vanilla reads the LivingEntity's main hand item here; before the item system is wired up it is always an empty stack
    public EntityCollisionContext(NetCraft.Registry.Entity entity, bool alwaysCollideWithFluid, bool placement)
        : this(entity.IsDescending(), placement, entity.Pos.Y, ItemStack.Empty, alwaysCollideWithFluid, entity) { }

    public NetCraft.Registry.Entity? GetEntity() => _entity;

    public override bool IsDescending() => _descending;

    public override bool IsAbove(VoxelShape shape, BlockPos pos, bool defaultValue)
        => _entityBottom > pos.Y + shape.Max(Direction.Axis.Y) - BelowTolerance;

    public override bool IsHoldingItem(Item item) => !_heldItem.IsEmpty() && ReferenceEquals(_heldItem.GetItem(), item);

    public override bool AlwaysCollideWithFluid() => _alwaysCollideWithFluid;

    //Vanilla requires the entity to be able to stand on the fluid and the fluid above to be of a different kind
    //The liquid support system (boats, ice, frost walker) is not wired up; returns no unconditionally for now
    public override bool CanStandOnFluid(FluidState fluidStateAbove, FluidState fluid) => false;

    public override VoxelShape GetCollisionShape(BlockState state, CollisionGetter getter, BlockPos pos)
        => state.GetCollisionShape(getter, pos, this);

    public override bool IsPlacement => _placement;

    //Empty empty context without an entity; the above check always uses the caller-provided default
    private sealed class Empty : EntityCollisionContext
    {
        public Empty(bool alwaysCollideWithFluid)
            : base(false, false, -double.MaxValue, ItemStack.Empty, alwaysCollideWithFluid, null) { }

        public override bool IsAbove(VoxelShape shape, BlockPos pos, bool defaultValue) => defaultValue;
    }
}
