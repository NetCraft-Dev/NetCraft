using System;
using NetCraft.Logging;
using NetCraft.Registry;

namespace NetCraft.Storage;

//ChunkSource, chunk source abstract base class, maps to vanilla net.minecraft.world.level.chunk.ChunkSource
//Provides interfaces to get chunks by ChunkPos and ChunkStatus; subclasses implement async scheduling
//Introduced in stage 11.48 to replace the synchronous wait in PersistentServerLevel.GetChunk
public abstract class ChunkSource : IDisposable
{
    private bool _disposed;

    //GetChunk gets the full chunk by chunkX/chunkZ, maps to vanilla getChunk
    //Returns null when not loaded
    public abstract ChunkAccess? GetChunk(int x, int z);

    //GetChunk gets the chunk by chunkX/chunkZ and ChunkStatus, maps to vanilla getChunk
    //When require is true, throws UnloadedChunkException if not loaded; when false, returns null
    public abstract ChunkAccess? GetChunk(int x, int z, ChunkStatus status, bool require);

    //HasChunk reports whether the chunk is loaded, maps to vanilla hasChunk
    public abstract bool HasChunk(int x, int z);

    //Tick advances chunk scheduling, maps to vanilla tick
    //Advances ChunkHolders; finished chunks move into the cache and idle holders are reclaimed
    public abstract void Tick();

    //Close releases resources, maps to vanilla close
    public virtual void Close() { }

    public void Dispose()
    {
        Log.Debug($"Dispose entry _disposed={_disposed}");
        if (_disposed)
        {
            Log.Debug("Dispose called again, skipping");
            return;
        }
        Dispose(true);
        _disposed = true;
        GC.SuppressFinalize(this);
        //Log.Debug($"Dispose exit");
    }

    protected virtual void Dispose(bool disposing) { }
}
