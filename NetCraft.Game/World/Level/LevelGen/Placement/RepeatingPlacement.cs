using NetCraft.Primitives;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//RepeatingPlacement repeating placement base, maps to vanilla RepeatingPlacement
//count decides how many times to repeat at the same position
public abstract class RepeatingPlacement : PlacementModifier
{
    //Count compute the repetition count, maps to vanilla count
    protected abstract int Count(RandomSource random, BlockPos origin);

    public override IEnumerable<BlockPos> GetPositions(PlacementContext context, RandomSource random, BlockPos origin)
    {
        var count = Count(random, origin);
        for (var i = 0; i < count; i++)
            yield return origin;
    }
}
