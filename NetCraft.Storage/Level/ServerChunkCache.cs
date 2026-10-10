using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetCraft.Config;
using NetCraft.Logging;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage.Light;

namespace NetCraft.Storage;

//ServerChunkCache, server chunk cache, maps to vanilla net.minecraft.server.level.ServerChunkCache
//Holds a ChunkMap and a load callback to schedule chunk loads asynchronously, avoiding synchronous blocking of the main loop
//Introduced in stage 11.48 to replace the synchronous wait in PersistentServerLevel.GetChunk
//Stage 11.52 adds the generator callback; on a save miss it goes through the ChunkStatus generation chain to create a new chunk
//The loader callback is passed in by PersistentServerLevel.LoadChunkAsync to avoid a circular dependency
public sealed class ServerChunkCache : ChunkSource
{
    private readonly ChunkMap _chunkMap;
    private readonly Func<ChunkPos, Task<ChunkAccess?>> _loader;
    //_ticketStorage, the chunk ticket save data, injected by the server; timeout clearing and force loading go through it
    private TicketStorage? _ticketStorage;
    //_loadingTracker, loading level propagation; the source is the loading tickets
    private LoadingChunkTracker? _loadingTracker;
    //_simulationTracker, simulation level propagation; the source is the simulation tickets
    private SimulationChunkTracker? _simulationTracker;
    //_playerCenters, each player's last view center and view distance; tickets are recomputed only on a chunk change or view distance change
    private readonly Dictionary<object, (int X, int Z, int ViewDistance, int SimulationLevel)> _playerCenters = new();
    //_loadingTicketRefs, player refcounts for chunks in view distance; tickets are actually removed only when it hits zero
    private readonly Dictionary<long, int> _loadingTicketRefs = new();
    //_simulationTicketRefs, refcounts for the chunks players are in; tickets are actually removed only when it hits zero
    private readonly Dictionary<long, int> _simulationTicketRefs = new();
    //generator, called on a save miss; passed by the Game layer and running through the ChunkStatusProcessor pipeline
    private readonly Func<ChunkPos, ChunkAccess?>? _generator;
    //Chunk generation and lighting run on background threads; the loaded cache is touched by both the main and generation threads and must be a concurrent dictionary
    private readonly ConcurrentDictionary<long, ChunkAccess> _loaded = new();
    //_generateGate, the generation concurrency gate; generation is a CPU-bound synchronous process
    //Two cores are held back rather than vanilla's one: one for the main thread, one for the light thread, which has nowhere else to run
    //Counting only the main thread left the machine over-subscribed once the light thread existed, and that slows down every thread at once
    private readonly SemaphoreSlim _generateGate = new(Math.Max(1, Environment.ProcessorCount - 2));
    //_lightEngine builds light data from chunk content once a chunk is ready; created lazily
    private readonly object _lightLock = new();
    //The light engine uses non-thread-safe dictionaries internally; vanilla runs it serially on a dedicated thread, and so does the light thread here
    //A semaphore rather than lock: the main-thread side only flushes, and taking it with a zero timeout lets the thread return to the pool instead of holding it
    //The light thread yields between rounds and never queues behind the main thread, which is what keeps the flush able to get in
    private readonly SemaphoreSlim _lightGate = new(1, 1);
    private ServerLightChunkGetter? _lightChunkGetter;
    private LevelLightEngine? _lightEngine;
    //Light changes pending dispatch; collects affected light section indices per chunk and dispatches them together after propagation, maps to vanilla ChunkMap.onLightUpdate
    //Only ever touched by the main thread: tick flushing and the block-change paths both run there
    private readonly Dictionary<ChunkPos, HashSet<int>> _pendingSkySections = new();
    private readonly Dictionary<ChunkPos, HashSet<int>> _pendingBlockSections = new();
    //Chunks that finished loading and are waiting for their light to be built; the light thread consumes them
    //Nothing on the generating side touches the light engine any more, so this is the only way light work enters
    private readonly ConcurrentQueue<ChunkAccess> _pendingLight = new();
    //Positions a block change made light-dirty; the light thread applies them
    //The main thread never touches the engine itself for a block change, which is what kept it fighting for the gate
    private readonly ConcurrentQueue<BlockPos> _pendingLightDirty = new();
    //Wakes the light thread; every producer releases once after queueing, and the thread re-signals itself while work remains
    private readonly SemaphoreSlim _lightWork = new(0, int.MaxValue);
    //The light thread, matching ScalableLux running lighting on its own executor rather than on the server or generating threads
    private Thread? _lightThread;
    //Chunks whose light is already built; the sender must not hand a chunk to a client before this, the client would render it pitch black
    //Concurrent because the sender reads it on its own thread while TickLight writes it under the gate
    private readonly ConcurrentDictionary<long, byte> _lightReady = new();
    //Set while the light thread builds light for freshly loaded chunks; their section changes are covered by the full light
    //block of the chunk packet, so they must not enter the pending set and be broadcast to every player
    [ThreadStatic] private static bool _suppressLightDispatch;
    //UnloadBudgetPerTick, at most this many chunks unload per tick, maps to the hasMoreTime budget of vanilla processUnloads
    //An unload takes a full chunk snapshot; without a limit, a player running far would unload a whole column in one tick and stall the main thread
    private const int UnloadBudgetPerTick = 16;

    //LightBatchBudget, queue entries per batch; too small means batches too dense and throughput lost, too large means longer main-thread waits
    private const int LightBatchBudget = 8192;

    //LightInitBudgetPerCycle, how many freshly loaded chunks the light thread lights before yielding the gate
    //Kept small so a single hold stays short: the main thread takes the gate with a zero timeout when it wants to flush
    private const int LightInitBudgetPerCycle = 1;

    //LightDirtyBudgetPerCycle, how many block-change positions the light thread applies before yielding the gate
    private const int LightDirtyBudgetPerCycle = 1024;

    //LightUpdateSink, the light change dispatch callback injected by the Game layer; args are the chunk pos and the affected section indices for sky/block light
    //The Storage layer cannot reach the player list and the packet is in the Game layer, corresponding to the layering difference of vanilla ChunkMap holding a ServerLevel
    public Action<ChunkPos, IReadOnlyList<int>, IReadOnlyList<int>>? LightUpdateSink { get; set; }

    //ChunkMap, the player view distance manager
    public ChunkMap ChunkMap => _chunkMap;

    //TicketStorage, the chunk ticket save data; null when not injected
    public TicketStorage? TicketStorage => _ticketStorage;

    //AttachTicketStorage wires up the chunk ticket save data and builds the two level trackers
    //Maps to vanilla ServerChunkCache running computeIfAbsent(TicketStorage.TYPE) at construction then handing it to ChunkMap
    //The trackers only enqueue ticket changes; the actual level advance is done by the convergence calls in tick
    public void AttachTicketStorage(TicketStorage storage)
    {
        _ticketStorage = storage;
        _loadingTracker = new LoadingChunkTracker(_chunkMap.Distance, storage);
        _simulationTracker = new SimulationChunkTracker(storage);
    }

    //SimulationDistance, how many chunks around a player entities can tick, maps to vanilla simulationDistance
    public int SimulationDistance { get; set; } = 10;

    //RunTicketTrackers immediately converges both ticket level sets to the current ticket table
    //Normally called once per tick by Tick; an explicit call is needed only when reading judgments right after issuing tickets
    public void RunTicketTrackers()
    {
        _simulationTracker?.RunAllUpdates();
        _loadingTracker?.RunDistanceUpdates(int.MaxValue);
    }

    //InEntityTickingRange, whether the chunk is within entity-ticking range, maps to vanilla inEntityTickingRange
    //Beyond simulation distance it loads but does not advance; this is the essence of "weak loading"
    //Before the ticket table is wired in, everything is tickable, keeping behavior consistent with before
    public bool InEntityTickingRange(long packedPos)
        => _simulationTracker is not { } tracker || ChunkLevel.IsEntityTicking(tracker.GetLevelAt(packedPos));

    //InBlockTickingRange, whether the chunk is within block-ticking range, maps to vanilla inBlockTickingRange
    //Scheduled ticks and block entity updates filter on it, one tier wider than the entity range
    public bool InBlockTickingRange(long packedPos)
        => _simulationTracker is not { } tracker || ChunkLevel.IsBlockTicking(tracker.GetLevelAt(packedPos));

    //UpdateChunkForced toggles force load, maps to vanilla ServerChunkCache.updateChunkForced
    public bool UpdateChunkForced(ChunkPos pos, bool add)
        => _ticketStorage?.UpdateChunkForced(pos, add) ?? false;

    //GetForceLoadedChunks, currently force-loaded chunks, maps to vanilla ServerChunkCache.getForceLoadedChunks
    public IReadOnlyCollection<long> GetForceLoadedChunks()
        => _ticketStorage?.GetForceLoadedChunks() ?? Array.Empty<long>();

    //HoldersCount, the current holder count, for diagnostics
    public int HoldersCount => _chunkMap.HoldersCount;

    //LoadedCount, the loaded cache size, for diagnostics
    public int LoadedCount => _loaded.Count;

    //Holders, a holder view so the GUI chunk map can take ticket levels and load progress per chunk without creating them
    public ICollection<ChunkHolder> Holders => _chunkMap.Holders;

    //ViewDistance, the view distance radius; the chunk map uses it to mark the load range boundary
    public int ViewDistance => _chunkMap.ViewDistance;

    //LoadedChunks, a view of loaded chunks for writes and random tick traversal
    //The concurrent dictionary's Values is a weakly consistent view; these two call sites take it every tick, so it is no longer copied into a list
    public ICollection<ChunkAccess> LoadedChunks => _loaded.Values;

    //ChunkLoaded, callback when a chunk first enters memory; the level uses it to load that chunk's entities into the entity manager
    public Action<ChunkPos>? ChunkLoaded { get; set; }

    //ChunkUnloaded, callback when a chunk leaves memory; the level uses it to clear block entities that lived with the chunk
    public Action<ChunkPos>? ChunkUnloaded { get; set; }

    //ChunkSaveSink, the write callback before a chunk leaves memory, injected by the level; without it an unload does not write
    //The implementation must take the snapshot synchronously; serialization and writing are its own async business and the unload does not wait
    public Action<ChunkAccess>? ChunkSaveSink { get; set; }

    //MinSectionY/SectionsCount, the world height range, default overworld -64..320
    public int MinSectionY { get; }

    public int SectionsCount { get; }

    //LightEngine, created lazily; it is both the light computation entry point and the data source for dispatch
    public LevelLightEngine LightEngine
    {
        get
        {
            if (_lightEngine is not null) return _lightEngine;
            lock (_lightLock)
            {
                //Pass a read-only lookup; neighbor fetches during light propagation must not trigger new loads or they recurse along the neighbor chain into a stack overflow
                _lightChunkGetter ??= new ServerLightChunkGetter(GetLoadedChunk, MinSectionY, SectionsCount)
                {
                    LightUpdateCallback = OnLightSectionUpdated,
                };
                _lightEngine ??= new LevelLightEngine(_lightChunkGetter, hasBlockLight: true, hasSkyLight: true);
                return _lightEngine;
            }
        }
    }

    public ServerChunkCache(Func<ChunkPos, Task<ChunkAccess?>> loader, int viewDistance = 8,
        Func<ChunkPos, ChunkAccess?>? generator = null, int minSectionY = -4, int sectionsCount = 24)
    {
        _loader = loader;
        _chunkMap = new ChunkMap(viewDistance);
        _generator = generator;
        MinSectionY = minSectionY;
        SectionsCount = sectionsCount;
    }

    //GetChunk gets the full chunk by chunkX/chunkZ, maps to vanilla getChunk
    //Main-loop safe: returns null when not loaded without blocking
    public override ChunkAccess? GetChunk(int x, int z)
        => GetChunk(x, z, ChunkStatus.FULL, false);

    //GetChunk gets the chunk by chunkX/chunkZ and ChunkStatus, maps to vanilla getChunk
    //When require is true, an unloaded chunk waits synchronously for the load to finish and then returns, throwing on failure; only for tests or scenarios that must be synchronous
    //When require is false, an unloaded chunk returns null without blocking the main loop
    public override ChunkAccess? GetChunk(int x, int z, ChunkStatus status, bool require)
    {
        var key = ChunkPos.Pack(x, z);
        if (_loaded.TryGetValue(key, out var cached))
            return cached;
        if (_chunkMap.GetHolder(key) is { IsDone: true } holder)
        {
            var result = holder.Future.Result;
            if (result.IsSuccess)
            {
                _loaded[key] = result.Chunk!;
                return result.Chunk;
            }
            if (require) throw result.Error!;
            return null;
        }
        if (require)
            return GetChunkFuture(x, z, status).GetAwaiter().GetResult().OrElse(null);
        //The non-blocking path must also submit the async load, or the chunk stays unloaded forever
        //The caller retries next tick; generation advances in the background without blocking the main loop
        _ = GetChunkFuture(x, z, status);
        return null;
    }

    //IsChunkFailed, whether the chunk is confirmed to have failed loading, as opposed to not yet ready
    //The sender uses it to drop permanently failed entries so the queue can drain
    public bool IsChunkFailed(int x, int z)
        => _chunkMap.GetHolder(ChunkPos.Pack(x, z)) is { IsDone: true } h
            && !h.Future.Result.IsSuccess;

    //GetLoadedChunk takes only loaded chunks without triggering a load, matching the read-only semantics of vanilla getChunkForLighting
    //A holder that is done but not yet merged into the cache is returned too; not ready returns null, which the light engine treats as fully opaque
    public ChunkAccess? GetLoadedChunk(int x, int z)
    {
        var key = ChunkPos.Pack(x, z);
        if (_loaded.TryGetValue(key, out var cached)) return cached;
        if (_chunkMap.GetHolder(key) is { IsDone: true } holder)
        {
            var result = holder.Future.Result;
            if (result.IsSuccess) return result.Chunk;
        }
        return null;
    }

    //IsLightReady reports whether the chunk's light has been built, meaning it is safe to send to a client
    //A chunk sent before that renders pitch black until an incremental light update arrives for every section
    public bool IsLightReady(ChunkPos pos) => _lightReady.ContainsKey(pos.Pack());

    //HasChunk reports whether the chunk is loaded, maps to vanilla hasChunk
    public override bool HasChunk(int x, int z)
    {
        Log.Debug($"HasChunk entry x={x} z={z}");
        var key = ChunkPos.Pack(x, z);
        var result = _loaded.ContainsKey(key)
            || (_chunkMap.GetHolder(key) is { IsDone: true } h && h.Future.Result.IsSuccess);
        Log.Debug($"HasChunk exit result={result}");
        return result;
    }

    //GetChunkFuture gets the chunk future asynchronously, maps to vanilla getChunkFuture
    //A loaded cache hit returns immediately; otherwise it submits the LoadAsync task
    public Task<ChunkResult> GetChunkFuture(int x, int z, ChunkStatus status)
    {
        var key = ChunkPos.Pack(x, z);
        if (_loaded.TryGetValue(key, out var cached))
            return Task.FromResult(ChunkResult.Success(cached));
        var holder = GetOrCreateHolder(x, z);
        if (holder.IsDone)
            return holder.Future;
        if (holder.MarkScheduled()) _ = LoadAsync(holder);
        return holder.Future;
    }

    //GetOrCreateHolder gets or creates a holder, maps to vanilla ChunkMap.getOrCreateHolder
    public ChunkHolder GetOrCreateHolder(int x, int z)
        => _chunkMap.GetOrCreateHolder(new ChunkPos(x, z));

    //TryGetHolder queries a holder without creating one, maps to vanilla getHolder
    public ChunkHolder? TryGetHolder(int x, int z)
        => _chunkMap.GetHolder(ChunkPos.Pack(x, z));

    //LoadAsync asynchronously loads a chunk and backfills the holder on success or failure, maps to vanilla schedule chunk load
    //A null from loader means the save has no such chunk, so generator runs the ChunkStatus generation chain to create a new chunk
    //When generator is also null, holder.Fail carries an UnloadedChunkException
    private async Task LoadAsync(ChunkHolder holder)
    {
        try
        {
            var chunk = await _loader(holder.Pos).ConfigureAwait(false);
            if (chunk is null && _generator is not null)
            {
                //Generation is a CPU-bound synchronous process; the gate caps concurrency at core count so the thread pool is not saturated at once
                await _generateGate.WaitAsync().ConfigureAwait(false);
                try { chunk = _generator(holder.Pos); }
                finally { _generateGate.Release(); }
            }
            if (chunk is null)
            {
                holder.Fail(new UnloadedChunkException($"Chunk {holder.Pos} not found in storage and no generator"));
            }
            else
            {
                //Light is not built here: the main thread does it on its own budget, see TickLight
                holder.Complete(chunk);
            }
        }
        catch (Exception e)
        {
            //Silently swallowing the exception would make a load failure look like a chunk that never sends; it must leave a trace
            Log.Warning($"Chunk load failed {holder.Pos}: {e}");
            holder.Fail(new UnloadedChunkException($"Failed to load chunk {holder.Pos}", e));
        }
    }

    //InitializeChunkLight builds light data from chunk content, matching the two stages initializeLight and lightChunk in vanilla
    //The order is fixed: mark section empty state -> enable light -> propagate light sources, as in vanilla; reversed, sky light preprocessing takes the wrong branch
    //Empty sections are registered too; otherwise a top empty section has no layer data and the sky light query takes the "always 15 above the data" shortcut
    //Unloaded neighbors are treated as fully opaque by the engine; that later neighbor loads do not retroactively recompute is a known simplification
    private void InitializeChunkLight(ChunkAccess chunk)
    {
        //The chunk is already in the loaded cache, so the light chunk getter builds its view on demand from there
        var engine = LightEngine;
        for (var sectionY = chunk.MinSectionY; sectionY <= chunk.MaxSectionY; sectionY++)
        {
            var section = chunk.GetSection(sectionY);
            engine.UpdateSectionStatus(new SectionPos(chunk.Pos.X, sectionY, chunk.Pos.Z),
                section is null || section.HasOnlyAir());
        }
        engine.SetLightEnabled(chunk.Pos, true);
        engine.PropagateLightSources(chunk.Pos);
        ReprocessLoadedNeighbors(chunk.Pos);
    }

    //ReprocessLoadedNeighbors recomputes loaded neighboring chunks, matching the vanilla LIGHT stage requirement that neighbors are ready
    //Here chunks advance to FULL in one go with no stage dependency, so the earlier-loaded side cannot see the later-loaded neighbor
    //If the engine cannot get a neighbor column's sky light source height it treats the border as having no source, so the border is too dark and never corrected later
    //Here only the neighbor's light sources are re-enqueued; actual propagation is left to the caller's propagation round
    private void ReprocessLoadedNeighbors(ChunkPos pos)
    {
        var engine = LightEngine;
        foreach (var neighbor in HorizontalNeighbors(pos))
        {
            //Unloaded neighbors need no recompute; they see this chunk when they load themselves
            //A neighbor still waiting for its own light is skipped too: its column is not enabled yet, so a scan would only re-queue
            //sources this very call already queued for it once it lights up
            if (!_lightReady.ContainsKey(neighbor.Pack())) continue;
            engine.PropagateLightSources(neighbor);
        }
    }

    //HorizontalNeighbors, the four horizontal neighbor chunk coords; sky light is affected by neighbor column heights only horizontally
    private static IEnumerable<ChunkPos> HorizontalNeighbors(ChunkPos pos)
    {
        yield return new ChunkPos(pos.X, pos.Z - 1);
        yield return new ChunkPos(pos.X, pos.Z + 1);
        yield return new ChunkPos(pos.X - 1, pos.Z);
        yield return new ChunkPos(pos.X + 1, pos.Z);
    }

    //OnLightSectionUpdated receives affected sections after a propagation round and accumulates them into the pending set
    //Forwarded by the light callback of ServerLightChunkGetter; the call site is inside light propagation and already holds the light lock
    private void OnLightSectionUpdated(LightLayer layer, SectionPos pos)
    {
        //A suppressed light-thread round is dropped here instead of the shared set being cleared afterwards, which is what the
        //old code did and how it used to swallow updates the main thread had computed but not yet sent
        if (_suppressLightDispatch) return;
        var engine = LightEngine;
        var index = pos.Y - engine.GetMinLightSection();
        if (index < 0 || index >= engine.GetLightSectionCount()) return;
        var target = layer == LightLayer.Sky ? _pendingSkySections : _pendingBlockSections;
        var chunk = pos.AsChunkPos();
        if (!target.TryGetValue(chunk, out var sections))
            target[chunk] = sections = new HashSet<int>();
        sections.Add(index);
    }

    //TakeLightUpdates moves the accumulated light changes per chunk out of the pending set, maps to the light broadcast at the end of a vanilla chunk tick
    //The caller must hold the light gate; sending is left to the caller so the packet is serialized outside the lock, which would otherwise hold the light thread back
    private List<(ChunkPos Pos, int[] Sky, int[] Block)>? TakeLightUpdates()
    {
        if (_pendingSkySections.Count == 0 && _pendingBlockSections.Count == 0) return null;
        var batch = new List<(ChunkPos, int[], int[])>(_pendingSkySections.Count + _pendingBlockSections.Count);
        var chunks = new HashSet<ChunkPos>(_pendingSkySections.Keys);
        chunks.UnionWith(_pendingBlockSections.Keys);
        foreach (var chunk in chunks)
        {
            _pendingSkySections.TryGetValue(chunk, out var sky);
            _pendingBlockSections.TryGetValue(chunk, out var block);
            batch.Add((chunk,
                sky is null ? Array.Empty<int>() : sky.ToArray(),
                block is null ? Array.Empty<int>() : block.ToArray()));
        }
        _pendingSkySections.Clear();
        _pendingBlockSections.Clear();
        return batch;
    }

    //UpdateLightBatch queues a batch of block changes for the light thread, matching vanilla running one propagation round per batch write
    //Per-cell marking that propagates each time repeatedly drains the queue; the thread applies the batch and runs one round at the end
    //The main thread only appends here, since touching the engine is what used to put it in contention with the light thread for the gate
    public void UpdateLightBatch(IReadOnlyList<BlockPos> positions)
    {
        if (positions.Count == 0) return;
        foreach (var pos in positions) _pendingLightDirty.Enqueue(pos);
        _lightWork.Release();
    }

    //MarkLightDirty marks a position's light dirty, maps to updateSectionStatus and checkBlock in vanilla LevelChunk.setBlockState
    //Only enqueues the node without propagating; the caller must already hold the light lock, so this only ever runs on the light thread
    private void MarkLightDirty(BlockPos pos)
    {
        var engine = LightEngine;
        //A section at the top that was fully empty has no light layer; when a block lands there, sync the section empty state first to create the layer
        var chunk = GetLoadedChunk(pos.X >> 4, pos.Z >> 4);
        var section = chunk?.GetSection(pos.Y >> 4);
        engine.UpdateSectionStatus(new SectionPos(pos.X >> 4, pos.Y >> 4, pos.Z >> 4),
            section is null || section.HasOnlyAir());
        //Then refresh that column's sky light source heightmap, otherwise the sky light engine still reads the pre-change occlusion height
        _lightChunkGetter?.UpdateSkyLightSources(pos);
        engine.CheckBlock(pos);
    }

    //UpdateLight queues a block state change for the light thread, which maps it onto updateSectionStatus and checkBlock
    //Nothing is marked here: the engine's section table is not thread-safe, and doing this on the main thread is a large part of why
    //the main thread and the light thread kept taking the gate from each other. Dispatch happens once per tick in TickLight
    //It also used to drain the propagation queue on every single write, turning a piston move into dozens of full propagation rounds
    public void UpdateLight(BlockPos pos)
    {
        _pendingLightDirty.Enqueue(pos);
        _lightWork.Release();
    }

    //TickLight sends the light changes the light thread computed since the last tick, maps to the light broadcast at the end of a vanilla chunk tick
    //The main thread no longer advances the engine at all: that belongs to the light thread, and the zero timeout means a busy light thread costs this
    //tick nothing but the flush, which then lands on the next tick. Light arriving one tick late is not noticeable, waiting for the gate was
    public void TickLight()
    {
        EnsureLightThread();
        var sink = LightUpdateSink;
        if (sink is null || !_lightGate.Wait(0)) return;

        List<(ChunkPos Pos, int[] Sky, int[] Block)>? batch = null;
        try
        {
            batch = TakeLightUpdates();
        }
        catch (Exception e)
        {
            Log.Warning($"Light flush failed: {e.Message}");
        }
        finally
        {
            _lightGate.Release();
        }

        //Serializing a whole light section is heavy and would keep the light thread waiting, so it happens outside the gate
        if (batch is null) return;
        foreach (var (pos, sky, block) in batch) sink(pos, sky, block);
    }

    //EnsureLightThread starts the light thread on first use; it lives until the process ends, mirroring the daemon executor ScalableLux uses
    //Lighting used to run on the generating threads, which held the gate across five full sky column scans per chunk and left the main thread
    //blocked on that gate every tick. The engine is serial either way, so one thread of its own is enough; what matters is that it is not the main thread
    private void EnsureLightThread()
    {
        if (_lightThread is not null) return;
        lock (_lightLock)
        {
            if (_lightThread is not null) return;
            _lightThread = new Thread(LightWorkerLoop)
            {
                IsBackground = true,
                Name = "netcraft-light",
                //Below normal so the light thread only ever takes a core nobody else wants: it is the one workload here that can wait
                //Generation and the main thread are both latency-bound, light arriving a tick later is not
                Priority = ThreadPriority.BelowNormal,
            };
            _lightThread.Start();
        }
    }

    //LightWorkerLoop drains light work until the process ends, in short rounds so the gate is never held for long
    //It waits on a signal rather than polling, so while idle it holds nothing at all, which is what lets the main thread take the gate with a zero timeout
    private void LightWorkerLoop()
    {
        while (true)
        {
            _lightWork.Wait();
            ProcessLightWork();
            //Hand the gate to whoever is waiting on it, the main thread's flush included
            //Sleep(0) rather than Yield: it also yields to threads at a different priority
            Thread.Sleep(0);
        }
    }

    //ProcessLightWork applies one round of queued light work and gives the gate back
    //The per-round budgets are deliberately small: a short hold is what keeps the main thread's zero-timeout flush able to get in at all
    private void ProcessLightWork()
    {
        if (!_lightGate.Wait(0))
        {
            //Someone else holds the gate; keep the work rather than dropping the wake-up, and come back shortly
            _lightWork.Release();
            Thread.Sleep(1);
            return;
        }

        var more = true;
        try
        {
            //Block changes first: their section changes have to reach clients, and one round is capped by LightBatchBudget
            var dirty = LightDirtyBudgetPerCycle;
            while (dirty-- > 0 && _pendingLightDirty.TryDequeue(out var pos)) MarkLightDirty(pos);
            if (LightEngine.HasLightWork()) LightEngine.RunLightUpdates(LightBatchBudget);

            //Freshly loaded chunks only once that queue has drained
            //Their propagation is covered by the full light block of the chunk packet, so dispatch is suppressed while it runs: without that,
            //every chunk load would broadcast the light of its whole neighbourhood to every player. The drain check is what keeps a block
            //change from being applied inside that suppressed window, where its update would never reach clients
            var init = LightInitBudgetPerCycle;
            while (!LightEngine.HasLightWork() && init-- > 0 && _pendingLight.TryDequeue(out var chunk))
            {
                //It may have been unloaded again while it waited in the queue
                if (!_loaded.ContainsKey(chunk.Pos.Pack())) continue;
                _suppressLightDispatch = true;
                try
                {
                    InitializeChunkLight(chunk);
                    LightEngine.RunLightUpdates(LightBatchBudget);
                }
                catch (Exception e)
                {
                    Log.Warning($"Chunk light failed {chunk.Pos}: {e.Message}");
                }
                finally
                {
                    _suppressLightDispatch = false;
                }
                _lightReady[chunk.Pos.Pack()] = 0;
            }

            //Decided under the gate, acted on after releasing it
            //The propagation queue has to count as remaining work too: a round is capped by LightBatchBudget, so a large queue can outlive
            //the two queues above and the thread would otherwise fall asleep with work still in the engine
            more = LightEngine.HasLightWork() || !_pendingLight.IsEmpty || !_pendingLightDirty.IsEmpty;
        }
        catch (Exception e)
        {
            Log.Warning($"Light worker failed: {e.Message}");
        }
        finally
        {
            _lightGate.Release();
        }

        //Still backed up: go around again rather than sleeping until some producer pushes more work
        if (more) _lightWork.Release();
    }

    //Tick advances chunk scheduling, maps to vanilla ServerChunkCache.tick
    //First clear timed-out tickets, then converge ticket levels into holder levels, and finally reclaim holders no longer needed by tickets
    public override void Tick()
    {
        //Log.Debug($"Tick entry holders={_chunkMap.HoldersCount} loaded={_loaded.Count}");
        //Timed-out tickets are cleared once per tick, maps to purgeStaleTickets in vanilla ServerChunkCache.tick
        _ticketStorage?.PurgeStaleTickets(ReadyForSaving);
        //Converge ticket levels into holder levels, maps to vanilla DistanceManager.runAllUpdates
        //Simulation then loading matches the vanilla order; the converged result is written into holders through DistanceManager
        _simulationTracker?.RunAllUpdates();
        _loadingTracker?.RunDistanceUpdates(int.MaxValue);
        _chunkMap.Distance.ChunksToUpdateFutures.Clear();
        List<long>? expired = null;
        foreach (var holder in _chunkMap.Holders)
        {
            var key = holder.Pos.Pack();
            if (holder.IsDone)
            {
                var result = holder.Future.Result;
                if (result.IsSuccess && !_loaded.ContainsKey(key))
                {
                    _loaded[key] = result.Chunk!;
                    //Light is built by the light thread, not here and not on the generating thread
                    //Building it on a generating thread meant holding the gate across five full sky column scans per chunk, with the main thread
                    //blocked on that gate once per tick; the chunk is held back from the sender until the light is ready
                    _pendingLight.Enqueue(result.Chunk!);
                    _lightWork.Release();
                    ChunkLoaded?.Invoke(result.Chunk!.Pos);
                }
            }
            //Holders whose tickets no longer require loading to FULL are reclaimed; loaded chunks stay in _loaded and remain usable
            //One currently loading is kept for now; dropping it means its future has nowhere to backfill the cache on completion
            //One still waiting for its light is kept too, or the work spent generating it would be thrown away
            //Judged by the ticket level rather than the holder's own level: the level is a BFS decay and takes several ticks to rise above the threshold after a ticket is removed
            //Here chunks generate straight to FULL with no intermediate state, so these shell holders serve no purpose and are best reclaimed early
            var loading = (holder.WasScheduled && !holder.IsDone)
                          || (_loaded.ContainsKey(key) && !_lightReady.ContainsKey(key));
            if (!loading && !IsLoadWanted(key, holder))
            {
                expired ??= new List<long>();
                expired.Add(key);
            }
        }
        if (expired is not null)
        {
            //Unloading a chunk snapshots and writes; too many in one tick stall the main thread, so over-budget ones wait for the next tick
            var budget = UnloadBudgetPerTick;
            foreach (var key in expired)
            {
                if (budget <= 0) break;
                if (UnloadChunkInternal(key)) budget--;
            }
        }
        //Log.Debug($"Tick exit");
    }

    //ReadyForSaving: whether the chunk's holder can safely drop tickets, matching the holder test of vanilla canTicketExpire
    //A missing holder or a done one both count as yes; while not ready the ticket is kept so the chunk does not lose tickets before it is written
    private bool ReadyForSaving(long packedPos)
        => _chunkMap.GetHolder(packedPos) is not { } holder || holder.IsDone;

    //IsLoadWanted: whether the chunk is still within the load range, matching the vanilla ChunkLevel.isLoaded test
    //Uses the holder's own level (the ticket after BFS propagation) rather than the raw ticket level
    //The ring just outside view distance has no ticket itself; judging by the raw ticket level would immediately treat them as reclaimable
    //when they are exactly the weak-loading band to keep; the threshold stays consistent with LoadingChunkTracker.SetLevel
    private bool IsLoadWanted(long packedPos, ChunkHolder holder)
        => holder.TicketLevel <= ChunkLevel.BlockTickingLevel;

    //ReleaseChunkLight frees all data a chunk left in the light engine when it leaves memory, maps to vanilla ThreadedLevelLightEngine.updateChunkStatus
    //The light engine stores data by section coords and does not know which chunk a section belongs to; without clearing on unload no one else will
    //Missing this step, the section table and data layers pile up with every chunk loaded, memory grows the longer you roam, and only Gen2 forced collection holds it together
    //First retract queued data then mark sections empty: marking empty drives the 26-neighbor counter to zero and only the finalize round actually drops the data layers
    private void ReleaseChunkLight(ChunkPos pos)
    {
        //The chunk is leaving memory, so drop its ready mark: if it comes back it has to be lit again before it may be sent
        _lightReady.TryRemove(pos.Pack(), out _);
        //A light engine never built means the chunk never computed light and there is nothing to return
        if (_lightEngine is null) return;
        _lightGate.Wait();
        try
        {
            _lightEngine.RetainData(pos, false);
            _lightEngine.SetLightEnabled(pos, false);
            //Light sections extend one above and below the world sections; queued data is cleared over the light section range
            for (var sectionY = _lightEngine.GetMinLightSection(); sectionY < _lightEngine.GetMaxLightSection(); sectionY++)
            {
                var lightSection = new SectionPos(pos.X, sectionY, pos.Z);
                _lightEngine.QueueSectionData(LightLayer.Block, lightSection, null);
                _lightEngine.QueueSectionData(LightLayer.Sky, lightSection, null);
            }
            for (var sectionY = MinSectionY; sectionY < MinSectionY + SectionsCount; sectionY++)
                _lightEngine.UpdateSectionStatus(new SectionPos(pos.X, sectionY, pos.Z), true);
            //The light view holds the whole chunk; without dropping it, an unloaded chunk is pinned in memory
            _lightChunkGetter?.DropView(pos.X, pos.Z);
        }
        finally
        {
            _lightGate.Release();
        }
    }

    //UnloadChunk moves a chunk out of memory and fires the unload callback, returns whether it actually unloaded
    //The caller must ensure the chunk is written, or in-memory changes are lost with the unload
    //This path does not write; ticket-driven automatic unloads go through UnloadChunkInternal
    public bool UnloadChunk(ChunkPos pos)
    {
        var key = pos.Pack();
        _chunkMap.TryRemoveHolder(key);
        if (!_loaded.TryRemove(key, out _)) return false;
        ReleaseChunkLight(pos);
        ChunkUnloaded?.Invoke(pos);
        return true;
    }

    //UnloadChunkInternal unloads a chunk tickets no longer require, returns whether a holder was actually touched
    //The order is fixed: detach holder -> write -> release light -> remove from memory -> notify the level
    //Detach the holder before writing: a ticket returning in the same tick only creates a new holder and cannot touch this batch already slated for discard
    //The write precedes light release: block entities and chunk data are still attached to the object at this point
    private bool UnloadChunkInternal(long packedPos)
    {
        if (!_chunkMap.TryRemoveHolder(packedPos)) return false;
        //A holder created without a loaded chunk is detached directly; there is no data to write
        if (!_loaded.TryRemove(packedPos, out var chunk)) return true;
        ChunkSaveSink?.Invoke(chunk);
        ReleaseChunkLight(chunk.Pos);
        ChunkUnloaded?.Invoke(chunk.Pos);
        return true;
    }

    //UpdatePlayerTickets updates a player's loading and simulation tickets when they change chunk or view distance
    //Maps to the ticket updates of PlayerTicketTracker and addPlayer in vanilla ChunkMap.move
    //Issues a PLAYER_LOADING ticket per chunk within view distance and a PLAYER_SIMULATION ticket on the player's chunk
    //Tickets are refcount-managed; when two players' view distances overlap, the first to leave does not affect the other
    public void UpdatePlayerTickets(object owner, int chunkX, int chunkZ, int viewDistance)
    {
        if (_ticketStorage is null) return;
        var radius = Math.Clamp(viewDistance, 2, 32);
        if (_playerCenters.TryGetValue(owner, out var previous)
            && previous.X == chunkX && previous.Z == chunkZ && previous.ViewDistance == radius) return;
        if (previous.ViewDistance > 0)
        {
            RemoveLoadingTickets(previous.X, previous.Z, previous.ViewDistance);
            RemoveSimulationTicket(ChunkPos.Pack(previous.X, previous.Z), previous.SimulationLevel);
        }
        //The simulation ticket level is computed only from simulation distance, maps to vanilla getPlayerTicketLevel
        //Not capped by view distance: a view distance smaller than simulation distance only narrows the load range, and the simulation level semantics should not change with it
        var simulationLevel = Math.Max(0, ChunkLevel.EntityTickingLevel - SimulationDistance);
        AddLoadingTickets(chunkX, chunkZ, radius);
        AddSimulationTicket(ChunkPos.Pack(chunkX, chunkZ), simulationLevel);
        _playerCenters[owner] = (chunkX, chunkZ, radius, simulationLevel);
    }

    //RemovePlayerTickets removes all of a player's tickets on leave, maps to vanilla ChunkMap.removePlayer
    public void RemovePlayerTickets(object owner)
    {
        if (_ticketStorage is null) return;
        if (!_playerCenters.Remove(owner, out var previous)) return;
        RemoveLoadingTickets(previous.X, previous.Z, previous.ViewDistance);
        RemoveSimulationTicket(ChunkPos.Pack(previous.X, previous.Z), previous.SimulationLevel);
    }

    //AddLoadingTickets adds a loading ticket per chunk in the view square; chunks already covered by another player only bump the count
    private void AddLoadingTickets(int centerX, int centerZ, int radius)
    {
        for (var dx = -radius; dx <= radius; dx++)
            for (var dz = -radius; dz <= radius; dz++)
            {
                var key = ChunkPos.Pack(centerX + dx, centerZ + dz);
                _loadingTicketRefs.TryGetValue(key, out var count);
                _loadingTicketRefs[key] = count + 1;
                if (count == 0)
                    _ticketStorage!.AddTicket(key, new Ticket(TicketType.PlayerLoading, ChunkLevel.EntityTickingLevel));
            }
    }

    //RemoveLoadingTickets removes loading tickets in the view square; if another player still covers it, only the count is decremented
    private void RemoveLoadingTickets(int centerX, int centerZ, int radius)
    {
        for (var dx = -radius; dx <= radius; dx++)
            for (var dz = -radius; dz <= radius; dz++)
            {
                var key = ChunkPos.Pack(centerX + dx, centerZ + dz);
                if (!_loadingTicketRefs.TryGetValue(key, out var count)) continue;
                if (count > 1)
                {
                    _loadingTicketRefs[key] = count - 1;
                    continue;
                }
                _loadingTicketRefs.Remove(key);
                _ticketStorage!.RemoveTicket(key, new Ticket(TicketType.PlayerLoading, ChunkLevel.EntityTickingLevel));
            }
    }

    //AddSimulationTicket adds a simulation ticket on the player's chunk
    private void AddSimulationTicket(long packedPos, int level)
    {
        _simulationTicketRefs.TryGetValue(packedPos, out var count);
        _simulationTicketRefs[packedPos] = count + 1;
        if (count == 0) _ticketStorage!.AddTicket(packedPos, new Ticket(TicketType.PlayerSimulation, level));
    }

    //RemoveSimulationTicket removes the simulation ticket on the player's chunk, using the level recorded at issue time
    private void RemoveSimulationTicket(long packedPos, int level)
    {
        if (!_simulationTicketRefs.TryGetValue(packedPos, out var count)) return;
        if (count > 1)
        {
            _simulationTicketRefs[packedPos] = count - 1;
            return;
        }
        _simulationTicketRefs.Remove(packedPos);
        _ticketStorage!.RemoveTicket(packedPos, new Ticket(TicketType.PlayerSimulation, level));
    }

    protected override void Dispose(bool disposing)
    {
        Log.Debug($"Dispose entry disposing={disposing}");
        if (disposing)
        {
            _chunkMap.ClearHolders();
            _loaded.Clear();
        }
        base.Dispose(disposing);
        //Log.Debug($"Dispose exit");
    }
}
