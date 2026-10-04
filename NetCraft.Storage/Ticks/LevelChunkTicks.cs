using NetCraft.Primitives;

namespace NetCraft.Storage.Ticks;

//ITickContainerAccess 区块级调度刻容器接口 对应原版 TickContainerAccess
public interface ITickContainerAccess<T> where T : class
{
    void Schedule(ScheduledTick<T> tick);
    bool HasScheduledTick(BlockPos pos, T type);
    bool WillTickThisTick(BlockPos pos, T type);
    int Count { get; }
}

//LevelChunkTicks 单区块的调度刻容器 对应原版 net.minecraft.world.tick.LevelChunkTicks
//优先队列加去重集合 同一位置同一类型只留最先排入的那个
//pendingTicks 是读档拿到但还没换算成绝对刻的那批 等区块开始 tick 时再展开
public sealed class LevelChunkTicks<T> : ITickContainerAccess<T> where T : class
{
    private List<SavedTick<T>>? _pendingTicks;
    private Action<LevelChunkTicks<T>, ScheduledTick<T>>? _onTickAdded;
    private readonly PriorityQueue<ScheduledTick<T>, ScheduledTick<T>> _tickQueue =
        new(ScheduledTick<T>.DrainOrder);
    private readonly HashSet<ScheduledTick<T>> _ticksPerPosition =
        new(new ScheduledTick<T>.UniqueTickComparer());

    public LevelChunkTicks() { }

    public LevelChunkTicks(List<SavedTick<T>> pendingTicks) => _pendingTicks = pendingTicks;

    //SetOnTickAdded 排入时回调 供 LevelTicks 维护容器索引
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

    //Poll 取出最早的一项并解除去重占位
    public ScheduledTick<T>? Poll()
    {
        if (!_tickQueue.TryDequeue(out var tick, out _)) return null;
        _ticksPerPosition.Remove(tick);
        return tick;
    }

    public ScheduledTick<T>? Peek()
        => _tickQueue.TryPeek(out var tick, out _) ? tick : null;

    //ScheduledTicks 已排队的全部刻 无序 供区域复制一类需要整表遍历的场合
    //待展开那批还没换算成绝对刻 不算在内
    public IEnumerable<ScheduledTick<T>> ScheduledTicks
    {
        get
        {
            foreach (var item in _tickQueue.UnorderedItems) yield return item.Element;
        }
    }

    public bool HasScheduledTick(BlockPos pos, T type)
        => _tickQueue.Count > 0 && _ticksPerPosition.Contains(Probe(type, pos));

    //WillTickThisTick 容器本身答不了"本刻会不会跑" 那个由 LevelTicks 统一回答
    public bool WillTickThisTick(BlockPos pos, T type) => false;

    //Pack 打包成存档形态 对应原版 pack
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

    //Unpack 把待展开的那批换算成绝对刻并正式排入 对应原版 unpack
    //子序号从负数往上递增 保证读档那批排在本次加载新排入的前面
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

    //Probe 造一个只用于查去重集合的哑对象 判重只看位置与类型
    private static ScheduledTick<T> Probe(T type, BlockPos pos)
        => new(type, pos, 0L, TickPriority.Normal, 0L);
}
