namespace NetCraft.Storage;

//DistanceManager, where ticket levels reach the holder, maps to vanilla net.minecraft.server.level.DistanceManager
//Only three rules needed: whether a chunk is pending removal, how to get a holder, and how to write a new level back
//3.3 defines the abstraction first; the fake host in tests and the real ChunkMap in 3.4 implement it separately
public abstract class DistanceManager
{
    //ChunksToUpdateFutures, holders whose level changed this round, handed up so generation can advance
    public List<ChunkHolder> ChunksToUpdateFutures { get; } = new();

    //IsChunkToRemove, whether the chunk is already in the pending-unload set, maps to vanilla isChunkToRemove
    public abstract bool IsChunkToRemove(long packedPos);

    //GetChunk returns the chunk holder, or null when absent, maps to vanilla getChunk
    public abstract ChunkHolder? GetChunk(long packedPos);

    //UpdateChunkScheduling writes the new level back and returns a holder that needs creating or reviving, maps to vanilla updateChunkScheduling
    public abstract ChunkHolder? UpdateChunkScheduling(long packedPos, int newLevel, ChunkHolder? holder, int oldLevel);
}
