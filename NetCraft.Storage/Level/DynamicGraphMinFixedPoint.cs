namespace NetCraft.Storage;

//DynamicGraphMinFixedPoint, distance graph min fixed-point propagation, maps to vanilla net.minecraft.world.level.lighting.DynamicGraphMinFixedPoint
//A node's level is the minimum of neighbor level plus one; when a value changes the node is enqueued by priority, and the main loop converges by ascending level
//Lighting and chunk tickets share this abstraction; subclasses only supply the three rules: read level, write level, and neighbor cost
public abstract class DynamicGraphMinFixedPoint
{
    //Source, the source node sentinel, maps to vanilla SOURCE; the value equals vanilla ChunkPos.INVALID_CHUNK_POS
    public const long Source = long.MaxValue;

    //NoComputedLevel, sentinel for an unrecorded computed level, maps to vanilla NO_COMPUTED_LEVEL
    private const int NoComputedLevel = 255;

    protected readonly int LevelCount;
    private readonly LeveledPriorityQueue _priorityQueue;
    //_computedLevels, computed levels pending convergence; absent means it is consistent with the current level
    private readonly Dictionary<long, int> _computedLevels;
    private bool _hasWork;

    protected DynamicGraphMinFixedPoint(int levelCount, int minQueueSize, int minMapSize)
    {
        if (levelCount >= 254) throw new ArgumentException("Level count must be less than 254", nameof(levelCount));
        LevelCount = levelCount;
        _priorityQueue = new LeveledPriorityQueue(levelCount, minQueueSize);
        _computedLevels = new Dictionary<long, int>(minMapSize);
    }

    //GetComputedLevel derives the node's level from neighbors, maps to vanilla getComputedLevel
    protected abstract int GetComputedLevel(long node, long knownParent, int knownLevelFromParent);
    //CheckNeighborsAfterUpdate propagates the effect to neighbors after a level change, maps to vanilla checkNeighborsAfterUpdate
    protected abstract void CheckNeighborsAfterUpdate(long node, int level, bool onlyDecrease);
    //GetLevel reads the node's current level
    protected abstract int GetLevel(long node);
    //SetLevel writes the node's level
    protected abstract void SetLevel(long node, int level);
    //ComputeLevelFromNeighbor computes this node's cost from a neighbor's level
    protected abstract int ComputeLevelFromNeighbor(long from, long to, int fromLevel);

    protected virtual bool IsSource(long node) => node == Source;

    //RemoveFromQueue removes the node from the queue, maps to vanilla removeFromQueue
    protected void RemoveFromQueue(long node)
    {
        if (!_computedLevels.Remove(node, out var computedLevel)) return;
        var priority = CalculatePriority(GetLevel(node), computedLevel);
        _priorityQueue.Dequeue(node, priority, LevelCount);
        _hasWork = !_priorityQueue.IsEmpty;
    }

    //RemoveIf removes in bulk by predicate, maps to vanilla removeIf
    public void RemoveIf(Func<long, bool> predicate)
    {
        List<long>? removed = null;
        foreach (var node in _computedLevels.Keys)
            if (predicate(node)) (removed ??= new List<long>()).Add(node);
        if (removed is null) return;
        foreach (var node in removed) RemoveFromQueue(node);
    }

    //CheckNode re-evaluates the node, maps to vanilla checkNode
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
            //If it was already consistent and needs no change, clear the record so the queue does not keep the same value
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
        //When the neighbor-derived level equals the known one, recompute from the loosest starting point so it gets a chance to converge lower
        if (levelFrom == oldComputedLevel)
            CheckEdge(from, to, LevelCount - 1,
                wasConsistent ? oldComputedLevel : GetLevel(to), storedOldComputedLevel, false);
    }

    protected bool HasWork => _hasWork;

    //RunUpdates main loop: take the head node, converge its level, then propagate to neighbors, maps to vanilla runUpdates
    //Returns the remaining budget; stops when the budget is used up or the queue is empty
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
                //When the computed level is higher than current, first lower the current to the loosest level, then let neighbors recompute
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

    //ComputedLevelAt returns 255 when unrecorded, matching the vanilla defaultReturnValue(-1) after a bitwise and
    private int ComputedLevelAt(long node)
        => _computedLevels.TryGetValue(node, out var level) ? level : NoComputedLevel;

    //TakeComputedLevel takes and removes the computed level, maps to vanilla computedLevels.remove(node) & 255
    private int TakeComputedLevel(long node)
        => _computedLevels.Remove(node, out var level) ? level : NoComputedLevel;
}
