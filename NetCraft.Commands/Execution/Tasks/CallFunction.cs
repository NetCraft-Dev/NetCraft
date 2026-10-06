using NetCraft.Commands.Functions;

namespace NetCraft.Commands.Execution.Tasks;

//CallFunction a single function call action, maps to vanilla net.minecraft.commands.execution.tasks.CallFunction
//Opens a new frame and enqueues the function entries one by one; returnParentFrame means discarding straight to the parent frame on return
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

    //Bind wraps it into an unbound action delegate
    public UnboundEntryAction<T> ToUnboundAction()
        => Execute;
}
