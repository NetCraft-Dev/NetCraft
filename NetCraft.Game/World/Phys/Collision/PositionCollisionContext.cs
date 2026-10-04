using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Phys.Collision;

//PositionCollisionContext 只按高度判定的碰撞上下文 对应原版 PositionCollisionContext
//无实体的位置查询用它 比如流体能否承载某高度的方块
public sealed class PositionCollisionContext : CollisionContext
{
    //原版那个常数是 (double)(float)1.0E-5 照抄不要改成 1.0E-5
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
