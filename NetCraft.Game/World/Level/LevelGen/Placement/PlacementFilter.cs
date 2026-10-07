using NetCraft.Primitives;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//PlacementFilter filter base, maps to vanilla PlacementFilter
//Keep the position when it passes, discard it otherwise
public abstract class PlacementFilter : PlacementModifier
{
    //ShouldPlace decide whether to keep the position, maps to vanilla shouldPlace
    protected abstract bool ShouldPlace(PlacementContext context, RandomSource random, BlockPos origin);

    public override IEnumerable<BlockPos> GetPositions(PlacementContext context, RandomSource random, BlockPos origin)
        => ShouldPlace(context, random, origin) ? new[] { origin } : Array.Empty<BlockPos>();
}
