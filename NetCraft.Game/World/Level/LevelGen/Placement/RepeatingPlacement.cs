using NetCraft.Primitives;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//RepeatingPlacement 重复放置基类对应原版 RepeatingPlacement
//按 count 决定在同一位置重复放几次
public abstract class RepeatingPlacement : PlacementModifier
{
    //Count 计算重复次数对应原版 count
    protected abstract int Count(RandomSource random, BlockPos origin);

    public override IEnumerable<BlockPos> GetPositions(PlacementContext context, RandomSource random, BlockPos origin)
    {
        var count = Count(random, origin);
        for (var i = 0; i < count; i++)
            yield return origin;
    }
}
