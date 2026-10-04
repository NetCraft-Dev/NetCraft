using NetCraft.Logging;
using NetCraft.Primitives;

namespace NetCraft.Storage.Ticks;

//LevelTicks 全关卡调度刻集合 对应原版 net.minecraft.world.tick.LevelTicks
//两层结构 每区块一个容器 外加一个按容器头元素排序的最小堆
//每 tick 分收集 执行 清理三步 收集数量受预算上限约束 收不满的留到下一 tick
public sealed class LevelTicks<T> where T : class
{
    private readonly Func<long, bool> _tickCheck;
    private readonly Dictionary<long, LevelChunkTicks<T>> _allContainers = new();
    //容器到它下一次该被检查的刻 用来跳过还没到点的容器
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

    //ContainerCount 已登记容器的区块数
    public int ContainerCount => _allContainers.Count;

    //AddContainer 区块开始参与 tick 时登记容器 对应原版 addContainer
    public void AddContainer(ChunkPos pos, LevelChunkTicks<T> container)
    {
        var key = ChunkPos.Pack(pos.X, pos.Z);
        _allContainers[key] = container;
        container.SetOnTickAdded(OnTickAdded);
        var head = container.Peek();
        if (head is not null) _nextTickForContainer[key] = head.TriggerTick;
    }

    //RemoveContainer 区块卸载时摘掉容器 对应原版 removeContainer
    public void RemoveContainer(ChunkPos pos)
    {
        var key = ChunkPos.Pack(pos.X, pos.Z);
        _allContainers.Remove(key);
        _nextTickForContainer.Remove(key);
    }

    //Schedule 排入一个调度刻 对应原版 schedule
    //目标区块没登记时丢弃并告警 原版在这里也是走 logAndPause
    public void Schedule(ScheduledTick<T> tick)
    {
        var key = ChunkPos.Pack(tick.Pos.X >> 4, tick.Pos.Z >> 4);
        if (_allContainers.TryGetValue(key, out var container)) container.Schedule(tick);
        else Log.Warning($"Tick scheduled on unloaded chunk {tick.Pos}");
    }

    public bool HasScheduledTick(BlockPos pos, T type)
        => _allContainers.TryGetValue(ChunkPos.Pack(pos.X >> 4, pos.Z >> 4), out var container)
            && container.HasScheduledTick(pos, type);

    //WillTickThisTick 本刻是否已经收集了该位置的刻
    public bool WillTickThisTick(BlockPos pos, T type)
    {
        CalculateTickSetIfNeeded();
        return _toRunThisTickSet.Contains(Probe(type, pos));
    }

    //CopyAreaFrom 把另一份表里落在矩形内的刻按 offset 平移搬进本表 对应原版 copyAreaFrom
    //clone 复制方块时要把源区正在跑的调度刻一并带过去 否则复制的红石与流体不会继续动
    //触发刻与子序号原样保留 同一刻内的先后关系不会被打乱
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

    //Tick 推进一刻 对应原版 LevelTicks.tick
    public void Tick(long currentTick, int maxTicksToProcess, Action<BlockPos, T> output)
    {
        CollectTicks(currentTick, maxTicksToProcess);
        RunCollectedTicks(output);
        CleanupAfterTick();
    }

    //OnTickAdded 只有新刻成为容器头时才需要更新索引
    private void OnTickAdded(LevelChunkTicks<T> container, ScheduledTick<T> tick)
    {
        if (!ReferenceEquals(tick, container.Peek())) return;
        _nextTickForContainer[ChunkPos.Pack(tick.Pos.X >> 4, tick.Pos.Z >> 4)] = tick.TriggerTick;
    }

    private bool CanScheduleMoreTicks(int maxTicksToProcess) => _toRunThisTick.Count < maxTicksToProcess;

    //CollectTicks 收集本刻该跑的刻 对应原版 collectTicks
    private void CollectTicks(long currentTick, int maxTicksToProcess)
    {
        SortContainersToTick(currentTick);
        DrainContainers(currentTick, maxTicksToProcess);
        RescheduleLeftoverContainers();
    }

    //SortContainersToTick 把已到点且允许 tick 的容器挑进待处理堆 对应原版同名方法
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
                //容器还早 索引推到它真正的下一个点
                _nextTickForContainer[key] = head.TriggerTick;
                continue;
            }
            //不在可 tick 范围的容器本刻跳过 索引留着下刻再看
            if (!_tickCheck(key)) continue;
            (consumed ??= new List<long>()).Add(key);
            _containersToTick.Enqueue(container, container);
        }

        if (consumed is null) return;
        foreach (var key in consumed) _nextTickForContainer.Remove(key);
    }

    //DrainContainers 按容器头归并取出本刻该跑的刻 对应原版 drainContainers
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

    //DrainFromCurrentContainer 从一个容器连续取 每取一项都与其它容器头比一次 保证同刻全局序
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

    //RescheduleLeftoverContainers 本刻没轮到的容器重新登记索引 对应原版同名方法
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

    //RunCollectedTicks 执行收集到的刻 对应原版 runCollectedTicks
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
