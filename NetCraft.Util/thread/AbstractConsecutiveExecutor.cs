using NetCraft.Logging;

namespace NetCraft.Util.Thread;

//连续执行器抽象基类对应原版AbstractConsecutiveExecutor
//状态机SLEEPING/RUNNING/CLOSED用CAS切换保证单线程串行执行
//核心机制schedule入队后CAS唤醒executor.Execute(this.Run)触发执行循环
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

    //executor回调入口每次只跑一个任务后回sleep再注册下次
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
