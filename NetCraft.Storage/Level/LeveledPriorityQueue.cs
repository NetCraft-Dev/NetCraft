namespace NetCraft.Storage;

//LeveledPriorityQueue, priority queue bucketed by level, maps to vanilla net.minecraft.world.level.lighting.LeveledPriorityQueue
//One order-preserving dedup set per level; the smallest non-empty bucket is the head, avoiding a global sort
//firstQueuedLevel tracks the current smallest non-empty bucket; when a bucket empties it scans forward once
public sealed class LeveledPriorityQueue
{
    private readonly int _levelCount;
    private readonly LongOrderedSet[] _queues;
    private int _firstQueuedLevel;

    public LeveledPriorityQueue(int levelCount, int minSize)
    {
        _levelCount = levelCount;
        _queues = new LongOrderedSet[levelCount];
        for (var i = 0; i < levelCount; i++) _queues[i] = new LongOrderedSet(minSize);
        _firstQueuedLevel = levelCount;
    }

    public bool IsEmpty => _firstQueuedLevel >= _levelCount;

    //RemoveFirst takes the head of the smallest-level bucket, maps to vanilla removeFirstLong
    public long RemoveFirst()
    {
        var queue = _queues[_firstQueuedLevel];
        var result = queue.RemoveFirst();
        if (queue.IsEmpty) CheckFirstQueuedLevel(_levelCount);
        return result;
    }

    //Enqueue, maps to vanilla enqueue
    public void Enqueue(long node, int key)
    {
        _queues[key].Add(node);
        if (_firstQueuedLevel > key) _firstQueuedLevel = key;
    }

    //Dequeue; if the one emptied is the current smallest-level bucket, scan forward, maps to vanilla dequeue
    public void Dequeue(long node, int key, int upperBound)
    {
        var queue = _queues[key];
        queue.Remove(node);
        if (queue.IsEmpty && _firstQueuedLevel == key) CheckFirstQueuedLevel(upperBound);
    }

    //CheckFirstQueuedLevel finds the first non-empty bucket after the old smallest level
    private void CheckFirstQueuedLevel(int upperBound)
    {
        var oldLevel = _firstQueuedLevel;
        _firstQueuedLevel = upperBound;
        for (var i = oldLevel + 1; i < upperBound; i++)
        {
            if (_queues[i].IsEmpty) continue;
            _firstQueuedLevel = i;
            return;
        }
    }
}
