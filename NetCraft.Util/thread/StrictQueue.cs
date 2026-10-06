using System.Collections.Concurrent;

namespace NetCraft.Util.Thread;

//Strict queue interface, maps to vanilla StrictQueue
//pop returns in enqueue or priority order, null means empty
public interface StrictQueue<T> where T : class
{
    T? Pop();
    bool Push(T task);
    bool IsEmpty { get; }
    int Size { get; }
}

//Prioritized runnable task, maps to vanilla RunnableWithPriority
//Lower priority value wins, 0 is highest
public sealed record RunnableWithPriority(int Priority, Action Task)
{
    public void Run() => Task();
}

//Plain sequential queue wrapping a ConcurrentQueue, maps to vanilla QueueStrictQueue
public sealed class QueueStrictQueue : StrictQueue<Action>
{
    private readonly ConcurrentQueue<Action> _queue = new();

    public Action? Pop() => _queue.TryDequeue(out var task) ? task : null;
    public bool Push(Action task) { _queue.Enqueue(task); return true; }
    public bool IsEmpty => _queue.IsEmpty;
    public int Size => _queue.Count;
}

//Bucketed-by-priority queue, maps to vanilla FixedPriorityQueue
//Internally multiple ConcurrentQueues indexed by priority for pop
public sealed class FixedPriorityQueue : StrictQueue<RunnableWithPriority>
{
    private readonly ConcurrentQueue<RunnableWithPriority>[] _queues;
    private int _size;

    public FixedPriorityQueue(int priorityCount)
    {
        _queues = new ConcurrentQueue<RunnableWithPriority>[priorityCount];
        for (var i = 0; i < priorityCount; i++)
            _queues[i] = new ConcurrentQueue<RunnableWithPriority>();
    }

    public RunnableWithPriority? Pop()
    {
        foreach (var queue in _queues)
        {
            if (queue.TryDequeue(out var task))
            {
                Interlocked.Decrement(ref _size);
                return task;
            }
        }
        return null;
    }

    public bool Push(RunnableWithPriority task)
    {
        if (task.Priority < 0 || task.Priority >= _queues.Length)
            throw new IndexOutOfRangeException($"Priority {task.Priority} not supported. Expected range [0-{_queues.Length - 1}]");
        _queues[task.Priority].Enqueue(task);
        Interlocked.Increment(ref _size);
        return true;
    }

    public bool IsEmpty => Volatile.Read(ref _size) == 0;
    public int Size => Volatile.Read(ref _size);
}
