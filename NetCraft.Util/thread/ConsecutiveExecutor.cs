namespace NetCraft.Util.Thread;

//简单顺序执行器对应原版ConsecutiveExecutor
//内部用QueueStrictQueue包装ConcurrentQueue按FIFO顺序执行
public sealed class ConsecutiveExecutor : AbstractConsecutiveExecutor<Action>
{
    public ConsecutiveExecutor(IExecutor executor, string name)
        : base(new QueueStrictQueue(), executor, name) { }

    protected override Action WrapRunnable(Action runnable) => runnable;
    protected override void RunTask(Action task) => task();
}
