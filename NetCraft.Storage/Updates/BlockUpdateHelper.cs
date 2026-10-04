using NetCraft.Primitives;
using NetCraft.Registry.State;

namespace NetCraft.Storage.Updates;

//BlockUpdateHelper 更新链上的公共动作 对应原版 Block 的静态方法
public static class BlockUpdateHelper
{
    //UpdateNeighbourShapes 向六方向发形状更新 对应原版 BlockStateBase.updateNeighbourShapes
    //传给邻接的方向取反向 因为邻接要看的是"从它指向本方块"
    public static void UpdateNeighbourShapes(ServerLevel level, BlockState state, BlockPos pos,
        int updateFlags, int updateLimit)
    {
        foreach (var direction in BlockUpdateFlags.ShapeUpdateOrder)
        {
            var neighbourPos = pos.Offset(direction);
            level.NeighborShapeChanged(direction.Opposite, neighbourPos, pos, state, updateFlags, updateLimit);
        }
    }

    //UpdateIndirectNeighbourShapes 间接形状更新 对应原版 updateIndirectNeighbourShapes
    public static void UpdateIndirectNeighbourShapes(ServerLevel level, BlockState state, BlockPos pos,
        int updateFlags, int updateLimit)
    {
        if (state.Owner is not IBlockUpdateBehaviour behaviour) return;
        behaviour.UpdateIndirectNeighbourShapes(level, pos, state, updateFlags, updateLimit);
    }

    //UpdateOrDestroy 形状结果为空则销毁否则写回 对应原版 Block.updateOrDestroy
    //没注入销毁出口时退化为直接置空 掉落会丢 只在无 Game 层的裸场景出现
    public static void UpdateOrDestroy(BlockState state, BlockState newState, ServerLevel level, BlockPos pos,
        int updateFlags, int updateLimit)
    {
        if (newState == state) return;
        var writeFlags = updateFlags & ~BlockUpdateFlags.SuppressDrops;
        if (!newState.Owner.IsAir)
        {
            level.SetBlock(pos, newState, writeFlags, updateLimit);
            return;
        }
        var sink = level.BlockUpdateSink;
        if (sink is not null)
            sink.DestroyBlock(pos, (updateFlags & BlockUpdateFlags.SuppressDrops) == 0, updateLimit);
        else
            level.SetBlock(pos, newState, writeFlags, updateLimit);
    }
}
