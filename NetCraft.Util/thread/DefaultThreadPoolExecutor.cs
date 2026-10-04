namespace NetCraft.Util.Thread;

//默认执行器复用.NET全局ThreadPool对应原版Util.ioPool
//简单跨平台零配置IO阻塞任务最多占一个线程因consecutiveExecutor串行
public sealed class DefaultThreadPoolExecutor : IExecutor
{
    public static readonly DefaultThreadPoolExecutor Instance = new();

    public string Name => "ThreadPool";

    private DefaultThreadPoolExecutor() { }

    public void Execute(Action task)
        => ThreadPool.QueueUserWorkItem(static state => ((Action)state!)(), task);
}
