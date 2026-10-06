namespace NetCraft.Storage;

//LoadingChunkTracker, loading level propagation, maps to vanilla net.minecraft.server.level.LoadingChunkTracker
//The source is the "loading" ticket level; the converged result is written back to DistanceManager holders
public sealed class LoadingChunkTracker : ChunkTracker
{
    //MaxLevel, the level used when there is no holder, one step above the vanilla MAX_LEVEL, maps to a vanilla local constant
    private const int MaxLevel = ChunkLevel.MaxLevel + 1;

    private readonly DistanceManager _distanceManager;
    private readonly TicketStorage _ticketStorage;

    public LoadingChunkTracker(DistanceManager distanceManager, TicketStorage ticketStorage)
        : base(MaxLevel + 1, 16, 256)
    {
        _distanceManager = distanceManager;
        _ticketStorage = ticketStorage;
        ticketStorage.SetLoadingChunkUpdatedListener((node, level, onlyDecreased) => Update(node, level, onlyDecreased));
    }

    protected override int GetLevelFromSource(long packedPos)
        => _ticketStorage.GetTicketLevelAt(packedPos, false);

    //GetLevel takes the holder's level if present; pending-removal or absent counts as not loaded
    protected override int GetLevel(long packedPos)
    {
        if (!_distanceManager.IsChunkToRemove(packedPos)
            && _distanceManager.GetChunk(packedPos) is { } holder)
            return holder.TicketLevel;
        return MaxLevel;
    }

    protected override void SetLevel(long packedPos, int level)
    {
        var holder = _distanceManager.GetChunk(packedPos);
        var oldLevel = holder?.TicketLevel ?? MaxLevel;
        if (oldLevel == level) return;
        //Only chunks still within the block-ticking tier after propagation get a new holder
        //The criterion must be the passed-in level (the BFS propagation result), not the raw ticket level:
        //The ring just outside view distance has no ticket itself, yet its level decays to 32 from neighbors, which is exactly the "load but do not tick" weak band
        //Blocking by the raw ticket level would erase this whole band, leaving only chunks of the same tier inside the view distance
        //Further out (33 and above) is only an empty shell of the load range; loading tickets are all 31 within view distance and the outer ring decaying from it has no practical use
        if (holder is null && level > ChunkLevel.BlockTickingLevel) return;
        if (_distanceManager.UpdateChunkScheduling(packedPos, level, holder, oldLevel) is { } updated)
            _distanceManager.ChunksToUpdateFutures.Add(updated);
    }

    //RunDistanceUpdates advances at most count nodes, maps to vanilla runDistanceUpdates
    public int RunDistanceUpdates(int count) => RunUpdates(count);
}
