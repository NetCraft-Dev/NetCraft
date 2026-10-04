using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;

namespace NetCraft.Storage.Updates;

//IBlockUpdateSink 更新链的 Game 层副作用出口 由 Game 层注入
//方块实体容器与掉落广播都是 Game 层职责 Storage 只负责在正确时机调用
public interface IBlockUpdateSink
{
    //RemoveBlockEntity 移除该位置的方块实体 返回是否确实移除了旧实体
    //对应原版 LevelChunk.setBlockState 里的 removeBlockEntity
    bool RemoveBlockEntity(BlockPos pos);

    //DestroyBlock 走完整销毁流程 掉落物与方块实体内容一并处理
    //形状更新算出空气时用它 对应原版 Level.destroyBlock
    void DestroyBlock(BlockPos pos, bool dropItems, int updateLimit);

    //BlockChanged 把方块变化同步给客户端 对应原版 flags 里的 UPDATE_CLIENTS
    //红石元件在行为回调里自己改状态时靠这条出口广播 否则客户端看不到变化
    void BlockChanged(BlockPos pos, BlockState state);

    //FlushBlockUpdates 把本拍积压的方块变化统一下发 对应原版 ChunkHolder.broadcastChanges
    //原版方块更新是记账后延迟到区块 tick 阶段才发的 方块事件包反而立即发
    //这个先后关系是活塞搬运能在客户端重放的前提 客户端重放时前方那格还得是原方块
    void FlushBlockUpdates();

    //LevelEvent 广播世界事件 对应原版 Level.levelEvent
    //红石火把烧毁那类纯表现事件走它 方块行为不直接持有玩家列表
    void LevelEvent(int kind, BlockPos pos, int data);

    //BlockEvent 广播方块事件 对应原版 Level.runBlockEvents 里 triggerEvent 成功后那一次广播
    //活塞伸缩与箱子开合的视觉全靠它 服务端算完事件不放出去客户端就一直停在旧样子
    void BlockEvent(BlockPos pos, NetCraft.Registry.Block block, int paramA, int paramB);

    //AddBlockEntity 为新写进去的方块创建方块实体 对应原版 LevelChunk 里的 newBlockEntity
    //方块实体容器在 Game 层 Storage 只负责在正确时机叫一声
    void AddBlockEntity(BlockPos pos, BlockState state);

    //GetBlockEntity 取该位置的方块实体 方块行为读自身可变状态用 没有返回 null
    //返回 object 是因为方块实体类型在 Game 层 Storage 不认识
    object? GetBlockEntity(BlockPos pos);

    //BlockEntityChanged 方块实体数据变了要同步给客户端
    void BlockEntityChanged(BlockPos pos);

    //SetBlockEntity 放入一个已经建好的方块实体 对应原版 Level.setBlockEntity
    //活塞推动时方块实体要带上被推状态与运动参数 走不了按状态新建那条路
    //entity 按 object 传 方块实体类型在 Game 层 Storage 不认识
    void SetBlockEntity(object entity);

    //PlaySound 在指定坐标播放音效 对应原版 Level.playSound
    //方块行为不持有玩家列表 音效由 Game 层出口广播
    void PlaySound(SoundEvent sound, SoundSource source, double x, double y, double z,
        float volume, float pitch);
}
