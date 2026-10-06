namespace NetCraft.Commands.Execution.Tasks;

//IsolatedCall 隔离帧调用对应原版 net.minecraft.commands.execution.tasks.IsolatedCall
//开一层新帧把控制权交给任务生产者 输出回调挂在新帧上 返回值不外泄
public class IsolatedCall<T>
{
    private readonly TaskProducer _taskProducer;
    private readonly CommandResultCallback _output;

    //TaskProducer 往隔离帧里排动作的生产者
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

    //Bind 包装成队列动作
    public EntryAction<T> ToEntryAction()
        => Execute;
}
