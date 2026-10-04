namespace NetCraft.Storage;

//DynamicGraphMinFixedPoint 距离图最小定点传播对应原版 net.minecraft.world.level.lighting.DynamicGraphMinFixedPoint
//节点等级取邻居等级加一的最小值 值一变就把节点按优先级入队 主循环按等级从小到大依次收敛
//光照与区块票共用这套抽象 子类只需给出取等级、写等级、邻居代价三条规则
public abstract class DynamicGraphMinFixedPoint
{
    //Source 源节点哨兵对应原版 SOURCE 取值与原版 ChunkPos.INVALID_CHUNK_POS 相同
    public const long Source = long.MaxValue;

    //NoComputedLevel 未记录计算等级的哨兵对应原版 NO_COMPUTED_LEVEL
    private const int NoComputedLevel = 255;

    protected readonly int LevelCount;
    private readonly LeveledPriorityQueue _priorityQueue;
    //_computedLevels 待收敛的计算等级 不存在即视为与当前等级一致
    private readonly Dictionary<long, int> _computedLevels;
    private bool _hasWork;

    protected DynamicGraphMinFixedPoint(int levelCount, int minQueueSize, int minMapSize)
    {
        if (levelCount >= 254) throw new ArgumentException("等级数必须小于 254", nameof(levelCount));
        LevelCount = levelCount;
        _priorityQueue = new LeveledPriorityQueue(levelCount, minQueueSize);
        _computedLevels = new Dictionary<long, int>(minMapSize);
    }

    //GetComputedLevel 由邻居推该节点应有的等级 对应原版 getComputedLevel
    protected abstract int GetComputedLevel(long node, long knownParent, int knownLevelFromParent);
    //CheckNeighborsAfterUpdate 等级变化后把影响传给邻居 对应原版 checkNeighborsAfterUpdate
    protected abstract void CheckNeighborsAfterUpdate(long node, int level, bool onlyDecrease);
    //GetLevel 读节点当前等级
    protected abstract int GetLevel(long node);
    //SetLevel 写节点等级
    protected abstract void SetLevel(long node, int level);
    //ComputeLevelFromNeighbor 由邻居等级算本节点代价
    protected abstract int ComputeLevelFromNeighbor(long from, long to, int fromLevel);

    protected virtual bool IsSource(long node) => node == Source;

    //RemoveFromQueue 把节点从队列摘掉 对应原版 removeFromQueue
    protected void RemoveFromQueue(long node)
    {
        if (!_computedLevels.Remove(node, out var computedLevel)) return;
        var priority = CalculatePriority(GetLevel(node), computedLevel);
        _priorityQueue.Dequeue(node, priority, LevelCount);
        _hasWork = !_priorityQueue.IsEmpty;
    }

    //RemoveIf 按谓词批量摘除 对应原版 removeIf
    public void RemoveIf(Func<long, bool> predicate)
    {
        List<long>? removed = null;
        foreach (var node in _computedLevels.Keys)
            if (predicate(node)) (removed ??= new List<long>()).Add(node);
        if (removed is null) return;
        foreach (var node in removed) RemoveFromQueue(node);
    }

    //CheckNode 重新评估该节点 对应原版 checkNode
    protected void CheckNode(long node) => CheckEdge(node, node, LevelCount - 1, false);

    protected void CheckEdge(long from, long to, int newLevelFrom, bool onlyDecreased)
    {
        CheckEdge(from, to, newLevelFrom, GetLevel(to), ComputedLevelAt(to), onlyDecreased);
        _hasWork = !_priorityQueue.IsEmpty;
    }

    private void CheckEdge(long from, long to, int newLevelFrom, int levelTo, int oldComputedLevel, bool onlyDecreased)
    {
        if (IsSource(to)) return;
        var clampedFrom = Math.Clamp(newLevelFrom, 0, LevelCount - 1);
        var clampedTo = Math.Clamp(levelTo, 0, LevelCount - 1);
        var wasConsistent = oldComputedLevel == NoComputedLevel;
        if (wasConsistent) oldComputedLevel = clampedTo;
        var newComputedLevel = onlyDecreased
            ? Math.Min(oldComputedLevel, clampedFrom)
            : Math.Clamp(GetComputedLevel(to, from, clampedFrom), 0, LevelCount - 1);
        var oldPriority = CalculatePriority(clampedTo, oldComputedLevel);
        if (clampedTo == newComputedLevel)
        {
            //本来一致又不需要变就清掉记录 免得队列里留着同一个值
            if (!wasConsistent)
            {
                _priorityQueue.Dequeue(to, oldPriority, LevelCount);
                _computedLevels.Remove(to);
            }
            return;
        }
        var newPriority = CalculatePriority(clampedTo, newComputedLevel);
        if (oldPriority != newPriority && !wasConsistent) _priorityQueue.Dequeue(to, oldPriority, newPriority);
        _priorityQueue.Enqueue(to, newPriority);
        _computedLevels[to] = newComputedLevel;
    }

    protected void CheckNeighbor(long from, long to, int level, bool onlyDecreased)
    {
        var storedOldComputedLevel = ComputedLevelAt(to);
        var levelFrom = Math.Clamp(ComputeLevelFromNeighbor(from, to, level), 0, LevelCount - 1);
        if (onlyDecreased)
        {
            CheckEdge(from, to, levelFrom, GetLevel(to), storedOldComputedLevel, true);
            return;
        }
        var wasConsistent = storedOldComputedLevel == NoComputedLevel;
        var oldComputedLevel = wasConsistent
            ? Math.Clamp(GetLevel(to), 0, LevelCount - 1)
            : storedOldComputedLevel;
        //邻居推出来的等级和已知的一致时用最宽松的起点重算一次 让它有机会往更低收敛
        if (levelFrom == oldComputedLevel)
            CheckEdge(from, to, LevelCount - 1,
                wasConsistent ? oldComputedLevel : GetLevel(to), storedOldComputedLevel, false);
    }

    protected bool HasWork => _hasWork;

    //RunUpdates 主循环 每次取队首节点收敛其等级再传播给邻居 对应原版 runUpdates
    //返回剩余配额 配额耗完或队列空即停
    protected int RunUpdates(int count)
    {
        if (_priorityQueue.IsEmpty) return count;
        while (!_priorityQueue.IsEmpty && count > 0)
        {
            count--;
            var node = _priorityQueue.RemoveFirst();
            var level = Math.Clamp(GetLevel(node), 0, LevelCount - 1);
            var computedLevel = TakeComputedLevel(node);
            if (computedLevel < level)
            {
                SetLevel(node, computedLevel);
                CheckNeighborsAfterUpdate(node, computedLevel, true);
            }
            else if (computedLevel > level)
            {
                //算出来的比当前高 先把当前降回最宽松等级再让邻居重算
                SetLevel(node, LevelCount - 1);
                if (computedLevel != LevelCount - 1)
                {
                    _priorityQueue.Enqueue(node, CalculatePriority(LevelCount - 1, computedLevel));
                    _computedLevels[node] = computedLevel;
                }
                CheckNeighborsAfterUpdate(node, level, false);
            }
        }
        _hasWork = !_priorityQueue.IsEmpty;
        return count;
    }

    public int QueueSize => _computedLevels.Count;

    private int CalculatePriority(int level, int computedLevel)
        => Math.Min(Math.Min(level, computedLevel), LevelCount - 1);

    //ComputedLevelAt 未记录时返回 255 与原版 defaultReturnValue(-1) 按位与的结果一致
    private int ComputedLevelAt(long node)
        => _computedLevels.TryGetValue(node, out var level) ? level : NoComputedLevel;

    //TakeComputedLevel 取出并移除计算等级 对应原版 computedLevels.remove(node) & 255
    private int TakeComputedLevel(long node)
        => _computedLevels.Remove(node, out var level) ? level : NoComputedLevel;
}
