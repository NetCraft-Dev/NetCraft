using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Phys.Collision;

//PositionCollisionContext collision context judged only by height, maps to vanilla PositionCollisionContext
//Used for entity-less position queries, such as whether a fluid can support a block at some height
public sealed class PositionCollisionContext : CollisionContext
{
    //Vanilla's constant is (double)(float)1.0E-5; copy it as is and do not change it to 1.0E-5
    private const double BelowTolerance = 9.999999747378752E-6;

    private readonly double _y;

    public PositionCollisionContext(double y) => _y = y;

    public override bool IsDescending() => false;

    public override bool IsAbove(VoxelShape shape, BlockPos pos, bool defaultValue)
        => _y > pos.Y + shape.Max(Direction.Axis.Y) - BelowTolerance;

    public override bool IsHoldingItem(Item item) => false;

    public override bool AlwaysCollideWithFluid() => false;

    public override bool CanStandOnFluid(FluidState fluidStateAbove, FluidState fluid) => false;

    public override VoxelShape GetCollisionShape(BlockState state, CollisionGetter getter, BlockPos pos)
        => state.GetCollisionShape(getter, pos, this);
}
