using NetCraft.Primitives;
using NetCraft.Registry.State;

namespace NetCraft.Storage.Updates;

//INeighborUpdater 更新分发器 对应原版 net.minecraft.world.level.redstone.NeighborUpdater
//两条通道共用一条队列 形状更新与邻居更新按同一套入队规则交错执行
public interface INeighborUpdater
{
    //ShapeUpdate 形状更新入队 对应原版 shapeUpdate
    void ShapeUpdate(Direction direction, BlockState neighbourState, BlockPos pos, BlockPos neighbourPos,
        int updateFlags, int updateLimit);

    //NeighborChanged 邻居更新入队 执行时重读该位置状态 对应原版三参 neighborChanged
    void NeighborChanged(BlockPos pos, NetCraft.Registry.Block changedBlock);

    //NeighborChanged 带状态快照的邻居更新 执行时不再重读 对应原版五参 neighborChanged
    void NeighborChanged(BlockPos pos, BlockState state, NetCraft.Registry.Block changedBlock, bool movedByPiston);

    //UpdateNeighborsAtExceptFromFacing 一次向六方向发邻居更新 可跳过指定方向
    //对应原版 updateNeighborsAtExceptFromFacing
    void UpdateNeighborsAtExceptFromFacing(BlockPos pos, NetCraft.Registry.Block block, Direction? skipDirection);
}
