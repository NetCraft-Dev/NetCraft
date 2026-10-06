using NetCraft.Commands.Functions;

namespace NetCraft.Commands.Execution.Tasks;

//CallFunction 一次函数调用动作对应原版 net.minecraft.commands.execution.tasks.CallFunction
//开新帧把函数条目逐条排进队列 returnParentFrame 表示返回时直接废弃到父帧
public class CallFunction<T>
{
    private readonly InstantiatedFunction<T> _function;
    private readonly CommandResultCallback _resultCallback;
    private readonly bool _returnParentFrame;

    public CallFunction(InstantiatedFunction<T> function, CommandResultCallback resultCallback, bool returnParentFrame)
    {
        _function = function;
        _resultCallback = resultCallback;
        _returnParentFrame = returnParentFrame;
    }

    public void Execute(T sender, ExecutionContext<T> context, Frame frame)
    {
        context.IncrementCost();
        var contents = _function.Entries;
        if (context.Tracer() is { } tracer)
        {
            tracer.OnCall(frame.Depth, _function.Id, contents.Count);
        }
        var newDepth = frame.Depth + 1;
        var frameControl = _returnParentFrame ? frame.FrameControl : context.FrameControlForDepth(newDepth);
        var newFrame = new Frame(newDepth, _resultCallback, frameControl);
        ContinuationTask<T, UnboundEntryAction<T>>.Schedule(context, newFrame, contents,
            (frame1, entryAction) => new CommandQueueEntry<T>(frame1, entryAction.Bind(sender)));
    }

    //Bind 包装成未绑定动作委托
    public UnboundEntryAction<T> ToUnboundAction()
        => Execute;
}
