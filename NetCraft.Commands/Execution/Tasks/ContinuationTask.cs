namespace NetCraft.Commands.Execution.Tasks;

//TaskProvider 按帧与参数造队列条目对应原版 ContinuationTask.TaskProvider
public delegate CommandQueueEntry<T> TaskProvider<T, P>(Frame frame, P argument);

//ContinuationTask 长列表的续跑动作对应原版 net.minecraft.commands.execution.tasks.ContinuationTask
//一到两个参数直接展开 三个以上自续跑逐个消费 免得一次性把整列表塞进队列
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

    //Schedule 排一串动作
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
