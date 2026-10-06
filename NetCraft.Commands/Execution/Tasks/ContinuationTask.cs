namespace NetCraft.Commands.Execution.Tasks;

//TaskProvider builds a queue entry from a frame and an argument; maps to vanilla ContinuationTask.TaskProvider
public delegate CommandQueueEntry<T> TaskProvider<T, P>(Frame frame, P argument);

//ContinuationTask continuation action for long lists, maps to vanilla net.minecraft.commands.execution.tasks.ContinuationTask
//One or two arguments expand directly; three or more self-continue one by one to avoid stuffing the whole list into the queue at once
public class ContinuationTask<T, P>
{
    private readonly TaskProvider<T, P> _taskFactory;
    private readonly List<P> _arguments;
    private readonly CommandQueueEntry<T> _selfEntry;
    private int _index;

    private ContinuationTask(TaskProvider<T, P> taskFactory, List<P> arguments, Frame frame)
    {
        _taskFactory = taskFactory;
        _arguments = arguments;
        _selfEntry = new CommandQueueEntry<T>(frame, Execute);
    }

    public void Execute(ExecutionContext<T> context, Frame frame)
    {
        var argument = _arguments[_index];
        context.QueueNext(_taskFactory(frame, argument));
        if (++_index < _arguments.Count)
        {
            context.QueueNext(_selfEntry);
        }
    }

    //Schedule enqueues a series of actions
    public static void Schedule(ExecutionContext<T> context, Frame frame, List<P> arguments, TaskProvider<T, P> taskFactory)
    {
        switch (arguments.Count)
        {
            case 0:
                break;
            case 1:
                context.QueueNext(taskFactory(frame, arguments[0]));
                break;
            case 2:
                context.QueueNext(taskFactory(frame, arguments[0]));
                context.QueueNext(taskFactory(frame, arguments[1]));
                break;
            default:
                context.QueueNext(new ContinuationTask<T, P>(taskFactory, arguments, frame)._selfEntry);
                break;
        }
    }
}
