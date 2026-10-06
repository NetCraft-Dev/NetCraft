using NetCraft.Logging;
using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage.Redstone;

namespace NetCraft.Storage.Updates;

//INeighborUpdate, queue item, maps to vanilla CollectingNeighborUpdater.NeighborUpdates
internal interface INeighborUpdate
{
    //RunNext runs one step, returns whether more steps remain
    bool RunNext(ServerLevel level);
}

//NeighborUpdater, update executor, maps to the static methods in vanilla NeighborUpdater
public static class NeighborUpdater
{
    //ExecuteUpdate runs one neighbor update, maps to vanilla executeUpdate
    //On exception, adds context and rethrows; vanilla throws ReportedException here, likewise crashing instead of swallowing
    public static void ExecuteUpdate(ServerLevel level, BlockState state, BlockPos pos,
        NetCraft.Registry.Block changedBlock, bool movedByPiston)
    {
        if (state.Owner is not IBlockUpdateBehaviour behaviour) return;
        //Whether the neighbor notification reached the component is the basis for judging "whether that hop was broken before scheduling"
        if (RedstoneIds.IsRedstoneComponent(state.Owner.Id))
            Log.Debug($"redstone neighbor notify {pos} {state.Owner.Id}:{state.Id} changed={changedBlock.Id}");
        try
        {
            behaviour.NeighborChanged(level, pos, state, changedBlock, movedByPiston);
        }
        catch (Exception e)
        {
            Log.Error($"Neighbor update failed {pos} {state.Owner.Id} changed={changedBlock.Id}: {e}");
            throw;
        }
    }

    //ExecuteShapeUpdate runs one shape update, maps to vanilla executeShapeUpdate
    //When the shape result is air it is destroyed, otherwise written back; decided by UpdateOrDestroy
    public static void ExecuteShapeUpdate(ServerLevel level, Direction direction, BlockPos pos, BlockPos neighbourPos,
        BlockState neighbourState, int updateFlags, int updateLimit)
    {
        var current = level.GetBlockState(pos);
        if (current is null) return;
        var state = current.Value;
        //Skip entirely when flag 128 is set and the target is redstone wire, maps to that vanilla branch
        if ((updateFlags & BlockUpdateFlags.SkipShapeUpdateOnWire) != 0 && state.Owner.Id == RedstoneIds.Wire)
            return;
        if (state.Owner is not IBlockUpdateBehaviour behaviour) return;
        try
        {
            var newState = behaviour.UpdateShape(level, pos, state, direction, neighbourPos, neighbourState);
            BlockUpdateHelper.UpdateOrDestroy(state, newState, level, pos, updateFlags, updateLimit);
        }
        catch (Exception e)
        {
            Log.Error($"Shape update failed {pos} {state.Owner.Id} target={neighbourPos}: {e}");
            throw;
        }
    }
}
