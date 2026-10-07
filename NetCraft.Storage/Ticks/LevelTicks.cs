using NetCraft.Logging;
using NetCraft.Primitives;

namespace NetCraft.Storage.Ticks;

//LevelTicks, the whole-level scheduled tick collection, maps to vanilla net.minecraft.world.tick.LevelTicks
//Two-layer structure: one container per chunk, plus a min-heap ordered by each container's head element
//Each tick has three steps: collect, run, cleanup; the collected count is bounded by a budget and leftovers wait for the next tick
public sealed class LevelTicks<T> : ILevelTickAccess<T> where T : class
{
    private readonly Func<long, bool> _tickCheck;
    private readonly Dictionary<long, LevelChunkTicks<T>> _allContainers = new();
    //Map from container to the next tick it should be checked, used to skip containers not yet due
    private readonly Dictionary<long, long> _nextTickForContainer = new();
    private readonly PriorityQueue<LevelChunkTicks<T>, LevelChunkTicks<T>> _containersToTick;
    private readonly Queue<ScheduledTick<T>> _toRunThisTick = new();
    private readonly List<ScheduledTick<T>> _alreadyRunThisTick = new();
    private readonly HashSet<ScheduledTick<T>> _toRunThisTickSet;

    public LevelTicks(Func<long, bool> tickCheck)
    {
        _tickCheck = tickCheck;
        _containersToTick = new PriorityQueue<LevelChunkTicks<T>, LevelChunkTicks<T>>(
            Comparer<LevelChunkTicks<T>>.Create(static (a, b) =>
            {
                var headA = a.Peek();
                var headB = b.Peek();
                if (headA is null) return headB is null ? 0 : 1;
                if (headB is null) return -1;
                return ScheduledTick<T>.IntraTickDrainOrder.Compare(headA, headB);
            }));
        _toRunThisTickSet = new HashSet<ScheduledTick<T>>(new ScheduledTick<T>.UniqueTickComparer());
    }

    //ContainerCount, the number of chunks with a registered container
    public int ContainerCount => _allContainers.Count;

    //Count, the total number of scheduled ticks across every container, maps to vanilla count
    public int Count
    {
        get
        {
            var total = 0;
            foreach (var container in _allContainers.Values) total += container.Count;
            return total;
        }
    }

    //AddContainer registers a container when a chunk starts ticking, maps to vanilla addContainer
    public void AddContainer(ChunkPos pos, LevelChunkTicks<T> container)
    {
        var key = ChunkPos.Pack(pos.X, pos.Z);
        _allContainers[key] = container;
        container.SetOnTickAdded(OnTickAdded);
        var head = container.Peek();
        if (head is not null) _nextTickForContainer[key] = head.TriggerTick;
    }

    //RemoveContainer detaches a container on chunk unload, maps to vanilla removeContainer
    public void RemoveContainer(ChunkPos pos)
    {
        var key = ChunkPos.Pack(pos.X, pos.Z);
        _allContainers.Remove(key);
        _nextTickForContainer.Remove(key);
    }

    //Schedule schedules a tick, maps to vanilla schedule
    //Dropped with a warning when the target chunk is not registered; vanilla also goes through logAndPause here
    public void Schedule(ScheduledTick<T> tick)
    {
        var key = ChunkPos.Pack(tick.Pos.X >> 4, tick.Pos.Z >> 4);
        if (_allContainers.TryGetValue(key, out var container)) container.Schedule(tick);
        else Log.Warning($"Tick scheduled on unloaded chunk {tick.Pos}");
    }

    public bool HasScheduledTick(BlockPos pos, T type)
        => _allContainers.TryGetValue(ChunkPos.Pack(pos.X >> 4, pos.Z >> 4), out var container)
            && container.HasScheduledTick(pos, type);

    //WillTickThisTick, whether this tick has already collected the tick at that pos
    public bool WillTickThisTick(BlockPos pos, T type)
    {
        CalculateTickSetIfNeeded();
        return _toRunThisTickSet.Contains(Probe(type, pos));
    }

    //CopyAreaFrom copies ticks falling inside the rectangle from another table into this one, shifted by offset, maps to vanilla copyAreaFrom
    //When clone copies blocks, the source area's running scheduled ticks must come along, or the copied redstone and fluids will not keep moving
    //Trigger tick and sub-order are kept as is, so within-tick ordering is not disturbed
    public void CopyAreaFrom(LevelTicks<T> from, int minX, int minY, int minZ, int maxX, int maxY, int maxZ,
        BlockPos offset)
    {
        foreach (var container in from._allContainers.Values)
        foreach (var tick in container.ScheduledTicks)
        {
            var pos = tick.Pos;
            if (pos.X < minX || pos.X > maxX) continue;
            if (pos.Y < minY || pos.Y > maxY) continue;
            if (pos.Z < minZ || pos.Z > maxZ) continue;
            Schedule(new ScheduledTick<T>(tick.Type,
                new BlockPos(pos.X + offset.X, pos.Y + offset.Y, pos.Z + offset.Z),
                tick.TriggerTick, tick.Priority, tick.SubTickOrder));
        }
    }

    //Tick advances one tick, maps to vanilla LevelTicks.tick
    public void Tick(long currentTick, int maxTicksToProcess, Action<BlockPos, T> output)
    {
        CollectTicks(currentTick, maxTicksToProcess);
        RunCollectedTicks(output);
        CleanupAfterTick();
    }

    //OnTickAdded: the index needs updating only when the new tick becomes the container head
    private void OnTickAdded(LevelChunkTicks<T> container, ScheduledTick<T> tick)
    {
        if (!ReferenceEquals(tick, container.Peek())) return;
        _nextTickForContainer[ChunkPos.Pack(tick.Pos.X >> 4, tick.Pos.Z >> 4)] = tick.TriggerTick;
    }

    private bool CanScheduleMoreTicks(int maxTicksToProcess) => _toRunThisTick.Count < maxTicksToProcess;

    //CollectTicks collects the ticks to run this tick, maps to vanilla collectTicks
    private void CollectTicks(long currentTick, int maxTicksToProcess)
    {
        SortContainersToTick(currentTick);
        DrainContainers(currentTick, maxTicksToProcess);
        RescheduleLeftoverContainers();
    }

    //SortContainersToTick picks due, tickable containers into the pending heap, maps to the vanilla same-named method
    private void SortContainersToTick(long currentTick)
    {
        List<long>? consumed = null;
        foreach (var (key, nextTick) in _nextTickForContainer)
        {
            if (nextTick > currentTick) continue;
            if (!_allContainers.TryGetValue(key, out var container))
            {
                (consumed ??= new List<long>()).Add(key);
                continue;
            }
            var head = container.Peek();
            if (head is null)
            {
                (consumed ??= new List<long>()).Add(key);
                continue;
            }
            if (head.TriggerTick > currentTick)
            {
                //The container is not due yet; push the index to its real next point
                _nextTickForContainer[key] = head.TriggerTick;
                continue;
            }
            //Containers outside the tickable range are skipped this tick; the index is kept for the next tick
            if (!_tickCheck(key)) continue;
            (consumed ??= new List<long>()).Add(key);
            _containersToTick.Enqueue(container, container);
        }

        if (consumed is null) return;
        foreach (var key in consumed) _nextTickForContainer.Remove(key);
    }

    //DrainContainers merges by container head and takes the ticks to run this tick, maps to vanilla drainContainers
    private void DrainContainers(long currentTick, int maxTicksToProcess)
    {
        while (CanScheduleMoreTicks(maxTicksToProcess)
            && _containersToTick.TryDequeue(out var top, out _))
        {
            var tick = top.Poll();
            if (tick is not null) ScheduleForThisTick(tick);
            DrainFromCurrentContainer(top, currentTick, maxTicksToProcess);
            var next = top.Peek();
            if (next is null) continue;
            if (next.TriggerTick <= currentTick && CanScheduleMoreTicks(maxTicksToProcess))
                _containersToTick.Enqueue(top, top);
            else
                _nextTickForContainer[ChunkPos.Pack(next.Pos.X >> 4, next.Pos.Z >> 4)] = next.TriggerTick;
        }
    }

    //DrainFromCurrentContainer takes repeatedly from one container, comparing against other heads each time to keep the global within-tick order
    private void DrainFromCurrentContainer(LevelChunkTicks<T> container, long currentTick, int maxTicksToProcess)
    {
        if (!CanScheduleMoreTicks(maxTicksToProcess)) return;
        if (!_containersToTick.TryPeek(out var nextBest, out _)) nextBest = null;
        var fromBest = nextBest?.Peek();
        while (CanScheduleMoreTicks(maxTicksToProcess))
        {
            var next = container.Peek();
            if (next is null || next.TriggerTick > currentTick) return;
            if (fromBest is not null && ScheduledTick<T>.IntraTickDrainOrder.Compare(next, fromBest) > 0) return;
            container.Poll();
            ScheduleForThisTick(next);
        }
    }

    //RescheduleLeftoverContainers re-registers indexes for containers not reached this tick, maps to the vanilla same-named method
    private void RescheduleLeftoverContainers()
    {
        while (_containersToTick.TryDequeue(out var container, out _))
        {
            var head = container.Peek();
            if (head is not null)
                _nextTickForContainer[ChunkPos.Pack(head.Pos.X >> 4, head.Pos.Z >> 4)] = head.TriggerTick;
        }
    }

    private void ScheduleForThisTick(ScheduledTick<T> tick)
    {
        _toRunThisTick.Enqueue(tick);
        _toRunThisTickSet.Add(tick);
    }

    //RunCollectedTicks runs the collected ticks, maps to vanilla runCollectedTicks
    private void RunCollectedTicks(Action<BlockPos, T> output)
    {
        while (_toRunThisTick.Count > 0)
        {
            var tick = _toRunThisTick.Dequeue();
            if (_toRunThisTickSet.Count > 0) _toRunThisTickSet.Remove(tick);
            _alreadyRunThisTick.Add(tick);
            output(tick.Pos, tick.Type);
        }
    }

    private void CleanupAfterTick()
    {
        _toRunThisTick.Clear();
        _containersToTick.Clear();
        _alreadyRunThisTick.Clear();
        _toRunThisTickSet.Clear();
    }

    private void CalculateTickSetIfNeeded()
    {
        if (_toRunThisTickSet.Count != 0 || _toRunThisTick.Count == 0) return;
        foreach (var tick in _toRunThisTick) _toRunThisTickSet.Add(tick);
    }

    private static ScheduledTick<T> Probe(T type, BlockPos pos)
        => new(type, pos, 0L, TickPriority.Normal, 0L);
}
