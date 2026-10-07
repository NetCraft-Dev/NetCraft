using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Level.LevelGen.BlockPredicates;

//StateTestingPredicate state-testing base, maps to vanilla StateTestingPredicate
//First fetch the block state at the offset, then let the subclass test the state itself
public abstract class StateTestingPredicate : BlockPredicate
{
    protected readonly Vec3i Offset;

    protected StateTestingPredicate(Vec3i offset) => Offset = offset;

    //Test tests only the block state, maps to vanilla test(BlockState)
    protected abstract bool Test(BlockState state);

    public sealed override bool Test(WorldGenRegion level, BlockPos origin)
    {
        var pos = origin.Offset(Offset);
        return Test(level.GetBlockState(pos.X, pos.Y, pos.Z));
    }

    //StateTestingCodec the offset field, maps to vanilla stateTestingCodec
    protected static MapCodec<Vec3i> StateTestingCodec()
        => Vec3iCodec.Offset16.OptionalFieldOf("offset", Vec3i.Zero);
}
