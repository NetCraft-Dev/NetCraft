namespace NetCraft.Storage;

//DistanceManager 票等级落到持有器的出口对应原版 net.minecraft.server.level.DistanceManager
//只需要三条规则: 区块是否待删、怎么取持有器、怎么把新等级写回去
//3.3 先定抽象 由测试里的假宿主与 3.4 的真实 ChunkMap 各自实现
public abstract class DistanceManager
{
    //ChunksToUpdateFutures 本轮等级变化过的持有器 交给上层推进生成任务
    public List<ChunkHolder> ChunksToUpdateFutures { get; } = new();

    //IsChunkToRemove 该区块是否已在待卸载集合 对应原版 isChunkToRemove
    public abstract bool IsChunkToRemove(long packedPos);

    //GetChunk 取该区块持有器 没有返回 null 对应原版 getChunk
    public abstract ChunkHolder? GetChunk(long packedPos);

    //UpdateChunkScheduling 写回新等级 需要新建或复活的持有器返回出来 对应原版 updateChunkScheduling
    public abstract ChunkHolder? UpdateChunkScheduling(long packedPos, int newLevel, ChunkHolder? holder, int oldLevel);
}
