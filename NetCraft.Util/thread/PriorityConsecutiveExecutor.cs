namespace NetCraft.Util.Thread;

//优先级连续执行器对应原版PriorityConsecutiveExecutor
//内部用FixedPriorityQueue按priorityCount个桶调度priority越小越优先
//scheduleWithResult返回Task由TaskCompletionSource控制完成
public sealed class PriorityConsecutiveExecutor : AbstractConsecutiveExecutor<RunnableWithPriority>
{
    public PriorityConsecutiveExecutor(int priorityCount, IExecutor executor, string name)
        : base(new FixedPriorityQueue(priorityCount), executor, name) { }

    protected override RunnableWithPriority WrapRunnable(Action runnable)
        => new(0, runnable);

    protected override void RunTask(RunnableWithPriority task) => task.Run();

    //提交带优先级的任务并返回Task由futureConsumer完成或异常
    public Task<TSource> ScheduleWithResult<TSource>(int priority, Action<TaskCompletionSource<TSource>> futureConsumer)
    {
        var tcs = new TaskCompletionSource<TSource>(TaskCreationOptions.RunContinuationsAsynchronously);
        Schedule(new RunnableWithPriority(priority, () => futureConsumer(tcs)));
        return tcs.Task;
    }
}
