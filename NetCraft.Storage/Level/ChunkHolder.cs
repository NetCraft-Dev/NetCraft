using System.Threading.Tasks;
using NetCraft.Logging;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Storage;

//ChunkHolder, chunk holder, maps to vanilla net.minecraft.server.level.ChunkHolder
//Holds the ChunkPos plus the current chunk load future and ticket level
//Lower ticket level means higher priority, aligning with vanilla ChunkHolderTicketLevel
//Introduced in stage 11.48 to replace the synchronous wait in PersistentServerLevel.GetChunk
public sealed class ChunkHolder
{
    //MaxLevel, the level at which nothing loads, maps to vanilla MAX_LEVEL
    public const int MaxLevel = ChunkLevel.MaxLevel;

    //BorderLevel, the load-only non-ticking level, i.e. vanilla FULL_CHUNK_LEVEL
    public const int BorderLevel = ChunkLevel.FullChunkLevel;

    //TickingLevel, the level at which blocks tick, i.e. vanilla BLOCK_TICKING_LEVEL
    public const int TickingLevel = ChunkLevel.BlockTickingLevel;

    //EntityTickingLevel, the level at which entities tick, i.e. vanilla ENTITY_TICKING_LEVEL
    public const int EntityTickingLevel = ChunkLevel.EntityTickingLevel;

    public ChunkPos Pos { get; }

    //TicketLevel, the current ticket level; lower means higher priority, maps to vanilla ticketLevel
    public int TicketLevel { get; private set; } = MaxLevel;

    private ChunkAccess? _chunk;
    private ChunkStatus _status = ChunkStatus.EMPTY;
    private readonly TaskCompletionSource<ChunkResult> _future =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _scheduled;

    //Chunk, the currently loaded chunk; returns null while loading
    public ChunkAccess? Chunk => _chunk;

    //Status, the current chunk status; returns EMPTY when not loaded
    public ChunkStatus Status => _status;

    //Future, the load completion future; success returns chunk, failure returns UnloadedChunkException
    public Task<ChunkResult> Future => _future.Task;

    //HasChunk, whether loading has completed
    public bool HasChunk => _chunk is not null;

    //IsDone, whether the future has completed
    public bool IsDone => _future.Task.IsCompleted;

    //WasScheduled, whether the load task was submitted, avoiding duplicate scheduling
    public bool WasScheduled => _scheduled;

    public ChunkHolder(ChunkPos pos)
    {
        Pos = pos;
    }

    //UpdateTicketLevel updates the ticket level and returns whether it changed, maps to vanilla setTicketLevel
    public bool UpdateTicketLevel(int level)
    {
        if (level == TicketLevel) return false;
        TicketLevel = level;
        return true;
    }

    //MarkScheduled marks the load task submitted and returns whether this was the first mark
    public bool MarkScheduled()
    {
        if (_scheduled) return false;
        _scheduled = true;
        return true;
    }

    //Complete sets the chunk and status once loading finishes and completes the future, maps to vanilla replaceProtoChunk
    public void Complete(ChunkAccess chunk)
    {
        //Log.Debug($"Complete entry chunk={chunk.Pos}");
        _chunk = chunk;
        _status = chunk.ChunkStatus;
        _future.TrySetResult(ChunkResult.Success(chunk));
        //Log.Debug($"Complete exit");
    }

    //Fail completes the future on load failure, maps to vanilla markForRemoval
    public void Fail(UnloadedChunkException error)
        => _future.TrySetResult(ChunkResult.Failure(error));
}
