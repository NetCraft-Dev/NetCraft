namespace NetCraft.Util.Thread;

//Priority consecutive executor, maps to vanilla PriorityConsecutiveExecutor
//Internally uses FixedPriorityQueue scheduling by priorityCount buckets, lower priority value wins
//scheduleWithResult returns a Task completed via TaskCompletionSource
public sealed class PriorityConsecutiveExecutor : AbstractConsecutiveExecutor<RunnableWithPriority>
{
    public PriorityConsecutiveExecutor(int priorityCount, IExecutor executor, string name)
        : base(new FixedPriorityQueue(priorityCount), executor, name) { }

    protected override RunnableWithPriority WrapRunnable(Action runnable)
        => new(0, runnable);

    protected override void RunTask(RunnableWithPriority task) => task.Run();

    //Submits a prioritized task and returns a Task, completed or faulted by futureConsumer
    public Task<TSource> ScheduleWithResult<TSource>(int priority, Action<TaskCompletionSource<TSource>> futureConsumer)
    {
        var tcs = new TaskCompletionSource<TSource>(TaskCreationOptions.RunContinuationsAsynchronously);
        Schedule(new RunnableWithPriority(priority, () => futureConsumer(tcs)));
        return tcs.Task;
    }
}
