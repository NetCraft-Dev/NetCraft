namespace NetCraft.Util.Thread;

//Default executor reuses the .NET global ThreadPool, maps to vanilla Util.ioPool
//Simple cross-platform zero-config; blocking IO tasks occupy at most one thread since the consecutiveExecutor is serial
public sealed class DefaultThreadPoolExecutor : IExecutor
{
    public static readonly DefaultThreadPoolExecutor Instance = new();

    public string Name => "ThreadPool";

    private DefaultThreadPoolExecutor() { }

    public void Execute(Action task)
        => ThreadPool.QueueUserWorkItem(static state => ((Action)state!)(), task);
}
