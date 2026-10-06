using System.Collections.Concurrent;
using System.Collections.Generic;
using NetCraft.Primitives;

namespace NetCraft.Storage;

//ChunkMap, chunk scheduling and view distance management, maps to vanilla net.minecraft.server.level.ChunkMap
//Holds the holder table and pending-unload set; the embedded DistanceManager lands ticket levels on holders
//Simplified in stage 11.48 to only track the player center chunk and view distance computation
//From 3.4, holder ownership moved here from ServerChunkCache, matching vanilla
public sealed class ChunkMap
{
    private readonly int _viewDistance;
    //_chunks, established holders; touched by both generation and main threads, so a concurrent dictionary is used
    private readonly ConcurrentDictionary<long, ChunkHolder> _chunks = new();
    //_toDrop, holders whose level fell out of the load range, maps to vanilla toDrop
    private readonly HashSet<long> _toDrop = new();
    //_pendingUnloads, holders marked for unload but not yet processed, maps to vanilla pendingUnloads
    private readonly Dictionary<long, ChunkHolder> _pendingUnloads = new();

    //ViewDistance, player view distance radius in chunks
    public int ViewDistance => _viewDistance;

    //Distance, the distance manager; the 3.3 BFS propagation lands ticket levels on holders through it
    public DistanceManager Distance { get; }

    public ChunkMap(int viewDistance)
    {
        //View distance lower bound 3, upper bound 32, aligning with vanilla server-view-distance
        _viewDistance = Math.Clamp(viewDistance, 3, 32);
        Distance = new ChunkMapDistanceManager(this);
    }

    //HoldersCount, the current holder count, for diagnostics
    public int HoldersCount => _chunks.Count;

    //Holders, a holder view for tick traversal and diagnostics
    public ICollection<ChunkHolder> Holders => _chunks.Values;

    //GetHolder gets a holder without creating one, maps to vanilla getUpdatingChunkIfPresent
    public ChunkHolder? GetHolder(long packedPos) => _chunks.GetValueOrDefault(packedPos);

    //GetOrCreateHolder gets or creates a holder, maps to vanilla getOrCreateHolder
    public ChunkHolder GetOrCreateHolder(ChunkPos pos)
        => _chunks.GetOrAdd(pos.Pack(), _ => new ChunkHolder(pos));

    //TryRemoveHolder removes a holder and returns whether it existed
    public bool TryRemoveHolder(long packedPos) => _chunks.TryRemove(packedPos, out _);

    //ClearHolders clears all holders
    public void ClearHolders() => _chunks.Clear();

    //UpdateChunkScheduling adjusts the holder to the new level, maps to vanilla updateChunkScheduling
    //If both old and new levels are outside the load range return as is; a level falling out is recorded for unload, and returning to range revives or creates
    public ChunkHolder? UpdateChunkScheduling(long packedPos, int level, ChunkHolder? holder, int oldLevel)
    {
        if (!ChunkLevel.IsLoaded(oldLevel) && !ChunkLevel.IsLoaded(level)) return holder;
        if (holder is null)
        {
            if (!ChunkLevel.IsLoaded(level)) return null;
            //First check the pending-unload set for an old holder of the same chunk; if present revive it to avoid reloading
            if (!_pendingUnloads.Remove(packedPos, out holder))
                holder = new ChunkHolder(ChunkPos.Unpack(packedPos));
            _chunks[packedPos] = holder;
        }
        holder.UpdateTicketLevel(level);
        //The pending-unload set follows the level; newly created holders must also be registered, otherwise after the level rises the old key stays in the set
        //The consequence is that LoadingChunkTracker.GetLevel keeps reporting the chunk as unloaded, and level convergence gets misjudged repeatedly
        if (ChunkLevel.IsLoaded(level)) _toDrop.Remove(packedPos);
        else _toDrop.Add(packedPos);
        return holder;
    }

    //IsChunkToRemove, whether the chunk is in the pending-unload set, maps to vanilla isChunkToRemove
    public bool IsChunkToRemove(long packedPos) => _toDrop.Contains(packedPos);

    //ChunkMapDistanceManager forwards the distance manager's three rules to this table, maps to the vanilla ChunkMap nested subclass
    private sealed class ChunkMapDistanceManager : DistanceManager
    {
        private readonly ChunkMap _map;

        public ChunkMapDistanceManager(ChunkMap map) => _map = map;

        public override bool IsChunkToRemove(long packedPos) => _map.IsChunkToRemove(packedPos);

        public override ChunkHolder? GetChunk(long packedPos) => _map.GetHolder(packedPos);

        public override ChunkHolder? UpdateChunkScheduling(long packedPos, int newLevel, ChunkHolder? holder, int oldLevel)
            => _map.UpdateChunkScheduling(packedPos, newLevel, holder, oldLevel);
    }
}
