using NetCraft.Primitives;

namespace NetCraft.Storage.Ticks;

//ITickContainerAccess, the chunk-level container shape, maps to vanilla TickContainerAccess
//Vanilla stops at the three members of TickAccess and keeps willTickThisTick on the level side; the extra member is
//kept here because LevelChunkTicks already exposed it, and reshaping the interface would ripple through callers
public interface ITickContainerAccess<T> : ITickAccess<T> where T : class
{
    bool WillTickThisTick(BlockPos pos, T type);
}

//LevelChunkTicks, scheduled tick container for one chunk, maps to vanilla net.minecraft.world.tick.LevelChunkTicks
//A priority queue plus a dedup set; the same pos and type keeps only the first scheduled one
//pendingTicks is the batch read from disk but not yet converted to absolute ticks; it is expanded once the chunk starts ticking
public sealed class LevelChunkTicks<T> : ITickContainerAccess<T>, ISerializableTickContainer<T> where T : class
{
    private List<SavedTick<T>>? _pendingTicks;
    private Action<LevelChunkTicks<T>, ScheduledTick<T>>? _onTickAdded;
    private readonly PriorityQueue<ScheduledTick<T>, ScheduledTick<T>> _tickQueue =
        new(ScheduledTick<T>.DrainOrder);
    private readonly HashSet<ScheduledTick<T>> _ticksPerPosition =
        new(new ScheduledTick<T>.UniqueTickComparer());

    public LevelChunkTicks() { }

    public LevelChunkTicks(List<SavedTick<T>> pendingTicks) => _pendingTicks = pendingTicks;

    //SetOnTickAdded, called on scheduling, so LevelTicks can maintain the container index
    public void SetOnTickAdded(Action<LevelChunkTicks<T>, ScheduledTick<T>>? onTickAdded)
        => _onTickAdded = onTickAdded;

    public int Count => _tickQueue.Count + (_pendingTicks?.Count ?? 0);

    public void Schedule(ScheduledTick<T> tick)
    {
        if (_ticksPerPosition.Add(tick)) ScheduleUnchecked(tick);
    }

    private void ScheduleUnchecked(ScheduledTick<T> tick)
    {
        _tickQueue.Enqueue(tick, tick);
        _onTickAdded?.Invoke(this, tick);
    }

    //Poll takes the earliest entry and clears its dedup slot
    public ScheduledTick<T>? Poll()
    {
        if (!_tickQueue.TryDequeue(out var tick, out _)) return null;
        _ticksPerPosition.Remove(tick);
        return tick;
    }

    public ScheduledTick<T>? Peek()
        => _tickQueue.TryPeek(out var tick, out _) ? tick : null;

    //ScheduledTicks, all scheduled ticks, unordered, for cases needing a full table walk like area copying
    //The unexpanded batch is not yet converted to absolute ticks and is not included
    public IEnumerable<ScheduledTick<T>> ScheduledTicks
    {
        get
        {
            foreach (var item in _tickQueue.UnorderedItems) yield return item.Element;
        }
    }

    public bool HasScheduledTick(BlockPos pos, T type)
        => _tickQueue.Count > 0 && _ticksPerPosition.Contains(Probe(type, pos));

    //WillTickThisTick: the container cannot answer "will it run this tick"; LevelTicks answers that uniformly
    public bool WillTickThisTick(BlockPos pos, T type) => false;

    //Pack packs into save form, maps to vanilla pack
    public List<SavedTick<T>> Pack(long currentTick)
    {
        var result = new List<SavedTick<T>>(_tickQueue.Count + (_pendingTicks?.Count ?? 0));
        if (_pendingTicks is not null) result.AddRange(_pendingTicks);
        var sorted = new List<ScheduledTick<T>>(_tickQueue.Count);
        foreach (var (element, _) in _tickQueue.UnorderedItems) sorted.Add(element);
        sorted.Sort(static (a, b) => a.SubTickOrder.CompareTo(b.SubTickOrder));
        foreach (var tick in sorted) result.Add(tick.ToSavedTick(currentTick));
        return result;
    }

    //Unpack converts the pending batch into absolute ticks and schedules them, maps to vanilla unpack
    //Sub-order increases from a negative base, ensuring the loaded batch sorts before newly scheduled ones
    public void Unpack(long currentTick)
    {
        if (_pendingTicks is null) return;
        var subTickBase = -(long)_pendingTicks.Count;
        foreach (var pending in _pendingTicks)
        {
            ScheduleUnchecked(pending.Unpack(currentTick, subTickBase));
            subTickBase++;
        }
        _pendingTicks = null;
    }

    //Probe builds a dummy object only for the dedup set lookup; dedup considers only pos and type
    private static ScheduledTick<T> Probe(T type, BlockPos pos)
        => new(type, pos, 0L, TickPriority.Normal, 0L);
}
