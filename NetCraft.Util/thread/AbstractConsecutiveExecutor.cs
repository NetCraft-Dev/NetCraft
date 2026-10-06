using NetCraft.Logging;

namespace NetCraft.Util.Thread;

//Consecutive executor abstract base class, maps to vanilla AbstractConsecutiveExecutor
//State machine SLEEPING/RUNNING/CLOSED switched by CAS to guarantee single-threaded serial execution
//Core mechanism: schedule enqueues then CAS-wakes executor.Execute(this.Run) to trigger the execution loop
public abstract class AbstractConsecutiveExecutor<T> where T : class
{
    private const int Sleeping = 0;
    private const int Running = 1;
    private const int Closed = 2;

    private volatile int _status = Sleeping;
    private readonly StrictQueue<T> _queue;
    private readonly IExecutor _executor;
    private readonly string _name;

    protected AbstractConsecutiveExecutor(StrictQueue<T> queue, IExecutor executor, string name)
    {
        _queue = queue;
        _executor = executor;
        _name = name;
    }

    public string Name => _name;
    public int Size => _queue.Size;
    public bool HasWork => _status == Running && !_queue.IsEmpty;
    protected bool IsRunning => _status == Running;
    protected bool IsClosed => _status == Closed;

    protected abstract T WrapRunnable(Action runnable);
    protected abstract void RunTask(T task);

    private bool CanBeScheduled() => !IsClosed && !_queue.IsEmpty;

    public void Close() => Interlocked.Exchange(ref _status, Closed);

    private bool PollTask()
    {
        if (_status != Running) return false;
        var task = _queue.Pop();
        if (task is null) return false;
        try { RunTask(task); }
        catch (Exception e) { Log.Exception(e, $"Error running task on {_name}"); }
        return true;
    }

    //The executor callback entry runs one task at a time then returns to sleep and registers the next
    public void Run()
    {
        try { PollTask(); }
        finally
        {
            Interlocked.CompareExchange(ref _status, Sleeping, Running);
            RegisterForExecution();
        }
    }

    public void RunAll()
    {
        do
        {
            Interlocked.CompareExchange(ref _status, Sleeping, Running);
            RegisterForExecution();
        } while (PollTask());
    }

    public void Schedule(T task)
    {
        _queue.Push(task);
        RegisterForExecution();
    }

    private void RegisterForExecution()
    {
        if (!CanBeScheduled()) return;
        if (Interlocked.CompareExchange(ref _status, Running, Sleeping) != Sleeping) return;
        try { _executor.Execute(Run); }
        catch (Exception)
        {
            try { _executor.Execute(Run); }
            catch (Exception e2) { Log.Exception(e2, $"Could not schedule ConsecutiveExecutor {_name}"); }
        }
    }

    public override string ToString() => $"{_name} {_status} {_queue.IsEmpty}";
}
