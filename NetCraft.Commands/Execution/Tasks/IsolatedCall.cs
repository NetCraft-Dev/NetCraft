namespace NetCraft.Commands.Execution.Tasks;

//IsolatedCall isolated frame call, maps to vanilla net.minecraft.commands.execution.tasks.IsolatedCall
//Opens a new frame and hands control to the task producer; the output callback hangs off the new frame and the return value does not leak
public class IsolatedCall<T>
{
    private readonly TaskProducer _taskProducer;
    private readonly CommandResultCallback _output;

    //TaskProducer producer that enqueues actions into the isolated frame
    public delegate void TaskProducer(ExecutionControl<T> output);

    public IsolatedCall(TaskProducer taskProducer, CommandResultCallback output)
    {
        _taskProducer = taskProducer;
        _output = output;
    }

    public void Execute(ExecutionContext<T> context, Frame frame)
    {
        var newFrameDepth = frame.Depth + 1;
        var newFrame = new Frame(newFrameDepth, _output, context.FrameControlForDepth(newFrameDepth));
        _taskProducer(ExecutionControl<T>.Create(context, newFrame));
    }

    //Bind wraps it into a queue action
    public EntryAction<T> ToEntryAction()
        => Execute;
}
