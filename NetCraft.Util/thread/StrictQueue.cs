using System.Collections.Concurrent;

namespace NetCraft.Util.Thread;

//严格队列接口对应原版StrictQueue
//pop按入队或优先级顺序返回null表示空
public interface StrictQueue<T> where T : class
{
    T? Pop();
    bool Push(T task);
    bool IsEmpty { get; }
    int Size { get; }
}

//带优先级的可运行任务对应原版RunnableWithPriority
//priority越小越优先0为最高
public sealed record RunnableWithPriority(int Priority, Action Task)
{
    public void Run() => Task();
}

//包装ConcurrentQueue的普通顺序队列对应原版QueueStrictQueue
public sealed class QueueStrictQueue : StrictQueue<Action>
{
    private readonly ConcurrentQueue<Action> _queue = new();

    public Action? Pop() => _queue.TryDequeue(out var task) ? task : null;
    public bool Push(Action task) { _queue.Enqueue(task); return true; }
    public bool IsEmpty => _queue.IsEmpty;
    public int Size => _queue.Count;
}

//按优先级分桶的队列对应原版FixedPriorityQueue
//内部多个ConcurrentQueue按优先级索引pop
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
