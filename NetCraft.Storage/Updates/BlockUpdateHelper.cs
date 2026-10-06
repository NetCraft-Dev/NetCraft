using NetCraft.Primitives;
using NetCraft.Registry.State;

namespace NetCraft.Storage.Updates;

//BlockUpdateHelper, common actions in the update chain, maps to the static methods on vanilla Block
public static class BlockUpdateHelper
{
    //UpdateNeighbourShapes sends shape updates in all six directions, maps to vanilla BlockStateBase.updateNeighbourShapes
    //The direction passed to the neighbor is reversed, because the neighbor needs "from itself toward this block"
    public static void UpdateNeighbourShapes(ServerLevel level, BlockState state, BlockPos pos,
        int updateFlags, int updateLimit)
    {
        foreach (var direction in BlockUpdateFlags.ShapeUpdateOrder)
        {
            var neighbourPos = pos.Offset(direction);
            level.NeighborShapeChanged(direction.Opposite, neighbourPos, pos, state, updateFlags, updateLimit);
        }
    }

    //UpdateIndirectNeighbourShapes, indirect shape update, maps to vanilla updateIndirectNeighbourShapes
    public static void UpdateIndirectNeighbourShapes(ServerLevel level, BlockState state, BlockPos pos,
        int updateFlags, int updateLimit)
    {
        if (state.Owner is not IBlockUpdateBehaviour behaviour) return;
        behaviour.UpdateIndirectNeighbourShapes(level, pos, state, updateFlags, updateLimit);
    }

    //UpdateOrDestroy destroys when the shape result is air, otherwise writes it back, maps to vanilla Block.updateOrDestroy
    //Falls back to just clearing when no destroy sink is injected; drops are lost, only occurs in bare scenarios without the Game layer
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
