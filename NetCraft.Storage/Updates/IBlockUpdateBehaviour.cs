using NetCraft.Primitives;
using NetCraft.Registry.State;

namespace NetCraft.Storage.Updates;

//IBlockUpdateBehaviour 方块更新行为契约 由 Game 层方块行为类实现
//Registry.Block 只承载数据 行为在 Game 层 更新链靠本接口回调 不反向依赖 Game
//方法签名与语义对齐原版 BlockBehaviour 的同名方法
public interface IBlockUpdateBehaviour
{
    //NeighborChanged 邻接方块变化后回调 对应原版 neighborChanged
    //changedBlock 是发生变化的那一方块 不是本方块
    //原版该签名还有 orientation 参数 非实验红石下恒为 null 接实验红石时再补参数位
    void NeighborChanged(ServerLevel level, BlockPos pos, BlockState state,
        NetCraft.Registry.Block changedBlock, bool movedByPiston);

    //UpdateShape 邻接方块形状变化后重算自身 对应原版 updateShape 默认返回原状态
    //directionToNeighbour 是从本方块指向邻接的方向
    BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
        Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState);

    //UpdateIndirectNeighbourShapes 间接形状更新 对应原版 updateIndirectNeighbourShapes 默认无行为
    void UpdateIndirectNeighbourShapes(ServerLevel level, BlockPos pos, BlockState state,
        int updateFlags, int updateLimit);

    //OnPlace 方块放置到位后回调 对应原版 onPlace
    void OnPlace(ServerLevel level, BlockPos pos, BlockState state, BlockState oldState, bool movedByPiston);

    //AffectNeighborsAfterRemoval 本方块被移除后对邻接的额外影响 对应原版 affectNeighborsAfterRemoval 默认无行为
    void AffectNeighborsAfterRemoval(ServerLevel level, BlockPos pos, BlockState state, bool movedByPiston);

    //Tick 调度刻回调 对应原版 Block.tick 由方块刻驱动调用
    void Tick(ServerLevel level, BlockPos pos, BlockState state, NetCraft.Util.Random.RandomSource random);

    //TriggerEvent 方块事件回调 返回是否已处理 对应原版 Block.triggerEvent
    bool TriggerEvent(ServerLevel level, BlockPos pos, BlockState state, int paramA, int paramB);
}
