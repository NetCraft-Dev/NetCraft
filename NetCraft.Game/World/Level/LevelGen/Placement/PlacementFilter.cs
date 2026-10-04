using NetCraft.Primitives;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//PlacementFilter 过滤基类对应原版 PlacementFilter
//通过则保留该位置 否则丢弃
public abstract class PlacementFilter : PlacementModifier
{
    //ShouldPlace 判定该位置是否保留对应原版 shouldPlace
    protected abstract bool ShouldPlace(PlacementContext context, RandomSource random, BlockPos origin);

    public override IEnumerable<BlockPos> GetPositions(PlacementContext context, RandomSource random, BlockPos origin)
        => ShouldPlace(context, random, origin) ? new[] { origin } : Array.Empty<BlockPos>();
}
