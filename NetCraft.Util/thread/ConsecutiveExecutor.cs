namespace NetCraft.Util.Thread;

//Simple sequential executor, maps to vanilla ConsecutiveExecutor
//Internally wraps a ConcurrentQueue with QueueStrictQueue, executing FIFO
public sealed class ConsecutiveExecutor : AbstractConsecutiveExecutor<Action>
{
    public ConsecutiveExecutor(IExecutor executor, string name)
        : base(new QueueStrictQueue(), executor, name) { }

    protected override Action WrapRunnable(Action runnable) => runnable;
    protected override void RunTask(Action task) => task();
}
