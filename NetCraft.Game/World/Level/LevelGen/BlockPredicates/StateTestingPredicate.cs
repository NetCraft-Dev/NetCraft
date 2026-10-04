using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Level.LevelGen.BlockPredicates;

//StateTestingPredicate 状态测试基类对应原版 StateTestingPredicate
//先按 offset 偏移取方块状态 再交给子类判定状态本身
public abstract class StateTestingPredicate : BlockPredicate
{
    protected readonly Vec3i Offset;

    protected StateTestingPredicate(Vec3i offset) => Offset = offset;

    //Test 只判方块状态对应原版 test(BlockState)
    protected abstract bool Test(BlockState state);

    public sealed override bool Test(WorldGenRegion level, BlockPos origin)
    {
        var pos = origin.Offset(Offset);
        return Test(level.GetBlockState(pos.X, pos.Y, pos.Z));
    }

    //StateTestingCodec 偏移字段 对应原版 stateTestingCodec
    protected static MapCodec<Vec3i> StateTestingCodec()
        => Vec3iCodec.Offset16.OptionalFieldOf("offset", Vec3i.Zero);
}
