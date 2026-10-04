using System.Threading.Tasks;
using NetCraft.Logging;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Storage;

//ChunkHolder 区块持有器对应原版 net.minecraft.server.level.ChunkHolder
//持有 ChunkPos 与当前 chunk 加载 future 与 ticket level
//ticket level 越小优先级越高对齐原版 ChunkHolderTicketLevel
//阶段 11.48 引入替代 PersistentServerLevel.GetChunk 同步等待
public sealed class ChunkHolder
{
    //MaxLevel 完全不加载的等级对应原版 MAX_LEVEL
    public const int MaxLevel = ChunkLevel.MaxLevel;

    //BorderLevel 仅加载不参与 tick 的等级即原版 FULL_CHUNK_LEVEL
    public const int BorderLevel = ChunkLevel.FullChunkLevel;

    //TickingLevel 方块可 tick 的等级即原版 BLOCK_TICKING_LEVEL
    public const int TickingLevel = ChunkLevel.BlockTickingLevel;

    //EntityTickingLevel 实体可 tick 的等级即原版 ENTITY_TICKING_LEVEL
    public const int EntityTickingLevel = ChunkLevel.EntityTickingLevel;

    public ChunkPos Pos { get; }

    //TicketLevel 当前 ticket 等级数值越小优先级越高对应原版 ticketLevel
    public int TicketLevel { get; private set; } = MaxLevel;

    private ChunkAccess? _chunk;
    private ChunkStatus _status = ChunkStatus.EMPTY;
    private readonly TaskCompletionSource<ChunkResult> _future =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _scheduled;

    //Chunk 当前已加载区块加载中返回 null
    public ChunkAccess? Chunk => _chunk;

    //Status 当前区块状态未加载返回 EMPTY
    public ChunkStatus Status => _status;

    //Future 加载完成 future 成功返回 chunk 失败返回 UnloadedChunkException
    public Task<ChunkResult> Future => _future.Task;

    //HasChunk 是否已加载完成
    public bool HasChunk => _chunk is not null;

    //IsDone future 是否已完成
    public bool IsDone => _future.Task.IsCompleted;

    //WasScheduled 是否已提交加载任务避免重复调度
    public bool WasScheduled => _scheduled;

    public ChunkHolder(ChunkPos pos)
    {
        Pos = pos;
    }

    //UpdateTicketLevel 更新 ticket 等级返回是否变化对应原版 setTicketLevel
    public bool UpdateTicketLevel(int level)
    {
        if (level == TicketLevel) return false;
        TicketLevel = level;
        return true;
    }

    //MarkScheduled 标记已提交加载任务返回是否首次标记
    public bool MarkScheduled()
    {
        if (_scheduled) return false;
        _scheduled = true;
        return true;
    }

    //Complete 加载完成设置 chunk 与状态完成 future 对应原版 replaceProtoChunk
    public void Complete(ChunkAccess chunk)
    {
        //Log.Debug($"Complete 入口 chunk={chunk.Pos}");
        _chunk = chunk;
        _status = chunk.ChunkStatus;
        _future.TrySetResult(ChunkResult.Success(chunk));
        //Log.Debug($"Complete 出口");
    }

    //Fail 加载失败完成 future 对应原版 markForRemoval
    public void Fail(UnloadedChunkException error)
        => _future.TrySetResult(ChunkResult.Failure(error));
}
