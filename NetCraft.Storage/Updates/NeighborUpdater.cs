using NetCraft.Logging;
using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage.Redstone;

namespace NetCraft.Storage.Updates;

//INeighborUpdate 队列项 对应原版 CollectingNeighborUpdater.NeighborUpdates
internal interface INeighborUpdate
{
    //RunNext 执行一步 返回是否还有后续步骤
    bool RunNext(ServerLevel level);
}

//NeighborUpdater 更新执行体 对应原版 NeighborUpdater 里的静态方法
public static class NeighborUpdater
{
    //ExecuteUpdate 执行一次邻居更新 对应原版 executeUpdate
    //异常补上下文后原样抛出 原版在这里抛 ReportedException 语义同样是崩掉而不是吞掉
    public static void ExecuteUpdate(ServerLevel level, BlockState state, BlockPos pos,
        NetCraft.Registry.Block changedBlock, bool movedByPiston)
    {
        if (state.Owner is not IBlockUpdateBehaviour behaviour) return;
        //邻居通知有没有送到元件上 是判断"排刻前那一跳断没断"的依据
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

    //ExecuteShapeUpdate 执行一次形状更新 对应原版 executeShapeUpdate
    //形状结果为空时走销毁否则写回 由 UpdateOrDestroy 决定
    public static void ExecuteShapeUpdate(ServerLevel level, Direction direction, BlockPos pos, BlockPos neighbourPos,
        BlockState neighbourState, int updateFlags, int updateLimit)
    {
        var current = level.GetBlockState(pos);
        if (current is null) return;
        var state = current.Value;
        //flag 128 且目标是红石线时整个跳过 对应原版该分支
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
