namespace NetCraft.Storage;

//LeveledPriorityQueue 按等级分桶的优先队列对应原版 net.minecraft.world.level.lighting.LeveledPriorityQueue
//每个等级一个保序去重集合 取最小等级的非空桶即为队首 省掉全局排序开销
//firstQueuedLevel 记录当前最小非空桶 桶空时向后扫一次
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

    //RemoveFirst 取出最小等级桶的队首 对应原版 removeFirstLong
    public long RemoveFirst()
    {
        var queue = _queues[_firstQueuedLevel];
        var result = queue.RemoveFirst();
        if (queue.IsEmpty) CheckFirstQueuedLevel(_levelCount);
        return result;
    }

    //Enqueue 入队对应原版 enqueue
    public void Enqueue(long node, int key)
    {
        _queues[key].Add(node);
        if (_firstQueuedLevel > key) _firstQueuedLevel = key;
    }

    //Dequeue 出队 若清空的正是当前最小等级桶就往后找 对应原版 dequeue
    public void Dequeue(long node, int key, int upperBound)
    {
        var queue = _queues[key];
        queue.Remove(node);
        if (queue.IsEmpty && _firstQueuedLevel == key) CheckFirstQueuedLevel(upperBound);
    }

    //CheckFirstQueuedLevel 从旧的最小等级之后开始找第一个非空桶
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
