using NetCraft.Codec;
using NetCraft.Config;
using NetCraft.Logging;
using NetCraft.Nbt;
using NetCraft.Nbt.Visitors;
using NetCraft.Primitives;
using NetCraft.Util;
using NetCraft.Util.Thread;

namespace NetCraft.Storage;

//Async chunk IO scheduler, maps to vanilla IOWorker
//Serializes the synchronous IO of RegionFileStorage through a PriorityConsecutiveExecutor
//pendingWrites merges multiple stores per ChunkPos into the last write
//Optimization 2.11: when the IoWorkerChannels switch is enabled it uses the Channels equivalent (PriorityConsecutiveExecutor is a lock-free serial queue, semantically equivalent to the Channels actor model)
//C2ME's rewrite of ChunkIoWorker has validated this actor-model approach
public sealed class IOWorker : IDisposable, ChunkScanAccess
{
    public const string IoWorkerNamePrefix = "IOWorker-";

    private readonly PriorityConsecutiveExecutor _executor;
    private readonly RegionFileStorage _storage;
    private volatile bool _shutdownRequested;
    private readonly LinkedList<KeyValuePair<ChunkPos, PendingStore>> _pendingOrder = new();
    private readonly Dictionary<ChunkPos, LinkedListNode<KeyValuePair<ChunkPos, PendingStore>>> _pendingIndex = new();
    //The region cache used for blending scans, maps to vanilla regionCacheForBlender
    //Long2ObjectLinkedOpenHashMap is simulated with a LinkedList + Dictionary, LRU capped at 1024
    private readonly LinkedList<KeyValuePair<long, Task<BitSet>>> _regionBlenderOrder = new();
    private readonly Dictionary<long, LinkedListNode<KeyValuePair<long, Task<BitSet>>>> _regionBlenderIndex = new();
    private const int RegionBlenderCacheSize = 1024;
    //The old-chunk threshold, maps to the 4882 hardcoded in vanilla isOldChunk
    //A DataVersion below this or the presence of the blending_data field counts as an old chunk
    private const int OldChunkDataVersion = 4882;

    public IOWorker(RegionStorageInfo info, string dir, bool sync)
        : this(info, dir, sync, DefaultThreadPoolExecutor.Instance) { }

    public IOWorker(RegionStorageInfo info, string dir, bool sync, IExecutor executor)
    {
        _storage = new RegionFileStorage(info, dir, sync);
        _executor = new PriorityConsecutiveExecutor(Enum.GetValues<Priority>().Length, executor,
            IoWorkerNamePrefix + info.Type);
    }

    private enum Priority
    {
        Foreground,
        Background,
        Shutdown
    }

    //Pending write cache, maps to vanilla PendingStore
    //Merges multiple stores of the same chunk into the last Data; Result fires when RunStore completes
    private sealed class PendingStore
    {
        public CompoundTag? Data;
        public readonly TaskCompletionSource Result = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public PendingStore(CompoundTag? data) { Data = data; }

        //Copy the data so external modification does not affect the pending content
        public CompoundTag? CopyData() => Data?.Copy() as CompoundTag;
    }

    //Whether an old chunk exists within range around pos, maps to vanilla isOldChunkAround
    //Scan all regions covering the area; if any bit is set, return true
    public bool IsOldChunkAround(ChunkPos pos, int range)
    {
        Log.Debug($"IsOldChunkAround entry pos={pos} range={range}");
        ChunkPos from = new(pos.X - range, pos.Z - range);
        ChunkPos to = new(pos.X + range, pos.Z + range);
        for (int regionX = from.GetRegionX(); regionX <= to.GetRegionX(); regionX++)
        {
            for (int regionZ = from.GetRegionZ(); regionZ <= to.GetRegionZ(); regionZ++)
            {
                BitSet data = GetOrCreateOldDataForRegion(regionX, regionZ).Result;
                if (!data.IsEmpty)
                {
                    ChunkPos minChunkPos = ChunkPos.MinFromRegion(regionX, regionZ);
                    int startChunkX = Math.Max(from.X - minChunkPos.X, 0);
                    int startChunkZ = Math.Max(from.Z - minChunkPos.Z, 0);
                    int endChunkX = Math.Min(to.X - minChunkPos.X, ChunkPos.RegionMaxIndex);
                    int endChunkZ = Math.Min(to.Z - minChunkPos.Z, ChunkPos.RegionMaxIndex);
                    for (int x = startChunkX; x <= endChunkX; x++)
                    {
                        for (int z = startChunkZ; z <= endChunkZ; z++)
                        {
                            int chunkIndex = (z * ChunkPos.RegionSize) + x;
                            if (data.Get(chunkIndex))
                            {
                                Log.Debug("IsOldChunkAround exit result=true");
                                return true;
                            }
                        }
                    }
                }
            }
        }
        Log.Debug("IsOldChunkAround exit result=false");
        return false;
    }

    //Get or create the region-level BitSet, maps to vanilla getOrCreateOldDataForRegion
    //An LRU cache hit returns early, otherwise it creates asynchronously and adds to the cache
    private Task<BitSet> GetOrCreateOldDataForRegion(int regionX, int regionZ)
    {
        long regionPos = ChunkPos.Pack(regionX, regionZ);
        lock (_regionBlenderOrder)
        {
            if (_regionBlenderIndex.TryGetValue(regionPos, out var node))
            {
                _regionBlenderOrder.Remove(node);
                _regionBlenderOrder.AddFirst(node);
                return node.Value.Value;
            }
            var task = CreateOldDataForRegion(regionX, regionZ);
            var newNode = new LinkedListNode<KeyValuePair<long, Task<BitSet>>>(new(regionPos, task));
            _regionBlenderOrder.AddFirst(newNode);
            _regionBlenderIndex[regionPos] = newNode;
            if (_regionBlenderOrder.Count > RegionBlenderCacheSize)
            {
                var last = _regionBlenderOrder.Last!;
                _regionBlenderOrder.RemoveLast();
                _regionBlenderIndex.Remove(last.Value.Key);
            }
            return task;
        }
    }

    //Scan the 1024 chunks in the region to build the old-chunk BitSet, maps to vanilla createOldDataForRegion
    //Uses CollectFields to take only DataVersion and blending_data, reducing IO cost
    private Task<BitSet> CreateOldDataForRegion(int regionX, int regionZ)
    {
        return Task.Run(() =>
        {
            ChunkPos from = ChunkPos.MinFromRegion(regionX, regionZ);
            ChunkPos to = ChunkPos.MaxFromRegion(regionX, regionZ);
            BitSet resultSet = new(ChunkPos.RegionSize * ChunkPos.RegionSize);
            foreach (var pos in ChunkPos.RangeClosed(from, to))
            {
                var collector = new CollectFields(
                    new FieldSelector(IntTag.IntTagType.Instance, SharedConstants.DataVersionTag),
                    new FieldSelector(CompoundTag.CompoundTagType.Instance, "blending_data"));
                try
                {
                    ScanChunk(pos, collector).Wait();
                    if (collector.GetResult() is CompoundTag chunkTag && IsOldChunk(chunkTag))
                    {
                        int chunkIndex = (pos.GetRegionLocalZ() * ChunkPos.RegionSize) + pos.GetRegionLocalX();
                        resultSet.Set(chunkIndex);
                    }
                }
                catch (Exception e)
                {
                    Log.Warning($"Failed to scan chunk {pos}");
                    Log.Exception(e);
                }
            }
            return resultSet;
        });
    }

    //Old chunk test, maps to vanilla isOldChunk
    //A DataVersion below the threshold or the presence of the blending_data field counts as an old chunk
    private bool IsOldChunk(CompoundTag tag)
    {
        if (NbtUtils.GetDataVersion(tag, 0) < OldChunkDataVersion) return true;
        return tag.Contains("blending_data");
    }

    public Task Store(ChunkPos pos, CompoundTag value) => Store(pos, () => value);

    public Task Store(ChunkPos pos, Func<CompoundTag> supplier)
    {
        Log.Debug($"Store entry pos={pos}");
        var result = UnwrapVoid(SubmitTask(() =>
        {
            var data = supplier();
            var store = GetOrCreatePendingStore(pos);
            store.Data = data;
            return store.Result.Task;
        }));
        //Log.Debug($"Store exit result={result}");
        return result;
    }

    public Task<Optional<CompoundTag>> LoadAsync(ChunkPos pos)
    {
        Log.Debug($"LoadAsync entry pos={pos}");
        var result = SubmitThrowingTask(() =>
        {
            if (_pendingIndex.TryGetValue(pos, out var node))
                return Optional<CompoundTag>.OfNullable(node.Value.Value.CopyData());
            try { return Optional<CompoundTag>.OfNullable(_storage.Read(pos)); }
            catch (Exception e)
            {
                Log.Warning($"Failed to read chunk {pos}");
                Log.Exception(e);
                throw;
            }
        });
        //Log.Debug($"LoadAsync exit result={result}");
        return result;
    }

    public async Task Synchronize(bool flush)
    {
        Log.Debug($"Synchronize entry flush={flush}");
        await UnwrapVoid(SubmitTask(() =>
        {
            var tasks = _pendingOrder.Select(p => p.Value.Result.Task).ToArray();
            return Task.WhenAll(tasks);
        })).ConfigureAwait(false);

        if (flush)
        {
            await SubmitThrowingTask<object?>(() =>
            {
                try { _storage.Flush(); return null; }
                catch (Exception e)
                {
                    Log.Warning("Failed to synchronize chunks");
                    Log.Exception(e);
                    throw;
                }
            }).ConfigureAwait(false);
        }
        //Log.Debug($"Synchronize exit");
    }

    public Task ScanChunk(ChunkPos pos, StreamTagVisitor visitor)
    {
        Log.Debug($"ScanChunk entry pos={pos}");
        var result = SubmitThrowingTask<object?>(() =>
        {
            try
            {
                if (_pendingIndex.TryGetValue(pos, out var node))
                {
                    var data = node.Value.Value.Data;
                    if (data is not null) ((Tag)data).AcceptAsRoot(visitor);
                    return null;
                }
                _storage.ScanChunk(pos, visitor);
                return null;
            }
            catch (Exception e)
            {
                Log.Warning($"Failed to bulk scan chunk {pos}");
                Log.Exception(e);
                throw;
            }
        });
        //Log.Debug($"ScanChunk exit result={result}");
        return result;
    }

    //Submit a task that may throw; the exception reaches the caller through the TaskCompletionSource
    private Task<T> SubmitThrowingTask<T>(Func<T> task)
        => _executor.ScheduleWithResult<T>((int)Priority.Foreground, tcs =>
        {
            if (!_shutdownRequested)
            {
                try { tcs.SetResult(task()); }
                catch (Exception e) { tcs.SetException(e); }
            }
            TellStorePending();
        });

    //Submit an ordinary task without catching exceptions; the caller is responsible
    private Task<T> SubmitTask<T>(Func<T> task)
        => _executor.ScheduleWithResult<T>((int)Priority.Foreground, tcs =>
        {
            if (!_shutdownRequested) tcs.SetResult(task());
            TellStorePending();
        });

    //Get or create the PendingStore; when it exists, only Data is updated without reordering
    private PendingStore GetOrCreatePendingStore(ChunkPos pos)
    {
        if (_pendingIndex.TryGetValue(pos, out var existing))
            return existing.Value.Value;
        var store = new PendingStore(null);
        var pair = new KeyValuePair<ChunkPos, PendingStore>(pos, store);
        var node = new LinkedListNode<KeyValuePair<ChunkPos, PendingStore>>(pair);
        _pendingOrder.AddLast(node);
        _pendingIndex[pos] = node;
        return store;
    }

    private void StorePendingChunk()
    {
        if (_pendingOrder.Count == 0) return;
        var node = _pendingOrder.First!;
        _pendingOrder.RemoveFirst();
        _pendingIndex.Remove(node.Value.Key);
        RunStore(node.Value.Key, node.Value.Value);
        TellStorePending();
    }

    private void TellStorePending()
        => _executor.Schedule(new RunnableWithPriority((int)Priority.Background, StorePendingChunk));

    private void RunStore(ChunkPos pos, PendingStore write)
    {
        try
        {
            _storage.Write(pos, write.Data);
            write.Result.SetResult();
        }
        catch (Exception e)
        {
            Log.Error($"Failed to store chunk {pos}");
            Log.Exception(e);
            write.Result.SetException(e);
        }
    }

    private Task WaitForShutdown()
        => _executor.ScheduleWithResult<object?>((int)Priority.Shutdown, tcs => tcs.SetResult(null));

    public RegionStorageInfo StorageInfo() => _storage.Info();

    //Unwrap a Task<Task> into a Task, maps to vanilla thenCompose(Function.identity())
    private static async Task UnwrapVoid(Task<Task> outer)
    {
        var inner = await outer.ConfigureAwait(false);
        await inner.ConfigureAwait(false);
    }

    public void Dispose()
    {
        //Log.Debug($"Dispose entry");
        if (Interlocked.CompareExchange(ref _shutdownRequested, true, false))
        {
            //Log.Debug($"Dispose exit");
            return;
        }
        try { WaitForShutdown().Wait(); }
        catch (Exception e) { Log.Exception(e, "Failed to wait for shutdown"); }
        _executor.Close();
        try { _storage.Close(); }
        catch (Exception e) { Log.Exception(e, "Failed to close storage"); }
        //Log.Debug($"Dispose exit");
    }
}
