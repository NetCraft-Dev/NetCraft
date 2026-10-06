namespace NetCraft.Util;

//SequencedPriorityIterator ordered priority queue, maps to vanilla net.minecraft.util.SequencedPriorityIterator
//Dequeue always takes the current highest priority, same priority preserves enqueue order; puzzle assembly relies on it to decide which piece to expand first
public sealed class SequencedPriorityIterator<T>
{
    //MinPriority empty priority sentinel, maps to vanilla MIN_PRIO
    private const int MinPriority = int.MinValue;

    private readonly Dictionary<int, Queue<T>> _queuesByPriority = new();
    private Queue<T>? _highestPriorityQueue;
    private int _highestPriority = MinPriority;

    //Count number of elements still in the queue
    public int Count { get; private set; }

    //Add enqueues; when priority equals the current highest it is appended at the tail, maps to vanilla add
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

    //HasNext whether any element remains to dequeue, maps to vanilla hasNext
    public bool HasNext => _highestPriorityQueue is { Count: > 0 };

    //Next dequeues the next element of the current highest priority, maps to vanilla next
    //Once the highest priority queue is drained it switches to the next-highest, so the next dequeue lands on the new queue
    public T Next()
    {
        var queue = _highestPriorityQueue;
        if (queue is null || queue.Count == 0) throw new InvalidOperationException("Queue is empty");
        Count--;
        var result = queue.Dequeue();
        if (queue.Count == 0) SwitchToNextHighestPriorityQueue();
        return result;
    }

    //SwitchToNextHighestPriorityQueue picks the next non-empty highest priority queue, maps to vanilla switchCacheToNextHighestPrioQueue
    private void SwitchToNextHighestPriorityQueue()
    {
        var foundPriority = MinPriority;
        Queue<T>? foundQueue = null;
        foreach (var (priority, queue) in _queuesByPriority)
        {
            if (priority <= foundPriority || queue.Count == 0) continue;
            foundPriority = priority;
            foundQueue = queue;
            //Once the adjacent next tier is found there is nothing higher, maps to vanilla early exit
            if (priority == _highestPriority - 1) break;
        }
        _highestPriority = foundPriority;
        _highestPriorityQueue = foundQueue;
    }
}
