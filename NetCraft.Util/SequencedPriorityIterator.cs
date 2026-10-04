namespace NetCraft.Util;

//SequencedPriorityIterator 定序优先级队列 对应原版 net.minecraft.util.SequencedPriorityIterator
//出队恒取当前最高优先级 同优先级保持入队先后 拼图装配靠它决定先展开哪个片段
public sealed class SequencedPriorityIterator<T>
{
    //MinPriority 空优先级哨兵 对应原版 MIN_PRIO
    private const int MinPriority = int.MinValue;

    private readonly Dictionary<int, Queue<T>> _queuesByPriority = new();
    private Queue<T>? _highestPriorityQueue;
    private int _highestPriority = MinPriority;

    //Count 还在队列里的元素个数
    public int Count { get; private set; }

    //Add 入队 优先级等于当前最高时直接接在队尾 对应原版 add
    public void Add(T data, int priority)
    {
        Count++;
        if (priority == _highestPriority && _highestPriorityQueue is not null)
        {
            _highestPriorityQueue.Enqueue(data);
            return;
        }
        if (!_queuesByPriority.TryGetValue(priority, out var queue))
        {
            queue = new Queue<T>();
            _queuesByPriority[priority] = queue;
        }
        queue.Enqueue(data);
        if (priority < _highestPriority) return;
        _highestPriorityQueue = queue;
        _highestPriority = priority;
    }

    //HasNext 是否还有待出队元素 对应原版 hasNext
    public bool HasNext => _highestPriorityQueue is { Count: > 0 };

    //Next 出队当前最高优先级的下一个 对应原版 next
    //取空最高优先级队列后立刻切到次高队列 下次出队就落在新队列上
    public T Next()
    {
        var queue = _highestPriorityQueue;
        if (queue is null || queue.Count == 0) throw new InvalidOperationException("队列已空");
        Count--;
        var result = queue.Dequeue();
        if (queue.Count == 0) SwitchToNextHighestPriorityQueue();
        return result;
    }

    //SwitchToNextHighestPriorityQueue 重新挑出非空的最高优先级队列 对应原版 switchCacheToNextHighestPrioQueue
    private void SwitchToNextHighestPriorityQueue()
    {
        var foundPriority = MinPriority;
        Queue<T>? foundQueue = null;
        foreach (var (priority, queue) in _queuesByPriority)
        {
            if (priority <= foundPriority || queue.Count == 0) continue;
            foundPriority = priority;
            foundQueue = queue;
            //已经找到紧邻的下一档就没有更高的了 对应原版提前跳出
            if (priority == _highestPriority - 1) break;
        }
        _highestPriority = foundPriority;
        _highestPriorityQueue = foundQueue;
    }
}
