using NetCraft.Commands.Context;
using NetCraft.Commands.Functions;
using NetCraft.Commands.Execution.Tasks;
using NetCraft.Logging;
using NetCraft.Util.Profiling;

namespace NetCraft.Commands.Execution;

//ExecutionContext command execution context, maps to vanilla net.minecraft.commands.execution.ExecutionContext
//Commands run one by one from the queue, each consuming quota; newly enqueued commands are inserted at the front to achieve depth-first order
//Both queue overflow and quota exhaustion trip a circuit breaker, with the threshold aligned with vanilla 10000000
public class ExecutionContext<T> : IDisposable
{
    //MaxQueueDepth queue depth circuit-breaker threshold
    public const int MaxQueueDepth = 10_000_000;

    private readonly int _commandLimit;
    private readonly int _forkLimit;
    private readonly ProfilerFiller _profiler;
    private TraceCallbacks? _tracer;
    private int _commandQuota;
    private bool _queueOverflow;
    private readonly LinkedList<CommandQueueEntry<T>> _commandQueue = new();
    private readonly List<CommandQueueEntry<T>> _newTopCommands = [];
    private int _currentFrameDepth;

    public ExecutionContext(int commandLimit, int forkLimit, ProfilerFiller profiler)
    {
        _commandLimit = commandLimit;
        _forkLimit = forkLimit;
        _profiler = profiler;
        _commandQuota = commandLimit;
    }

    //CreateTopFrame top frame; when already at the top, discarding means clearing the whole queue; maps to vanilla createTopFrame
    private static Frame CreateTopFrame(ExecutionContext<T> context, CommandResultCallback frameResult)
    {
        if (context._currentFrameDepth == 0)
        {
            return new Frame(0, frameResult, context._commandQueue.Clear);
        }
        var reentrantFrameDepth = context._currentFrameDepth + 1;
        return new Frame(reentrantFrameDepth, frameResult, context.FrameControlForDepth(reentrantFrameDepth));
    }

    //QueueInitialFunctionCall enqueues the first function call; maps to vanilla queueInitialFunctionCall
    public static void QueueInitialFunctionCall(ExecutionContext<T> context, InstantiatedFunction<T> function,
        T sender, CommandResultCallback functionReturn)
    {
        var callback = ((ExecutionSourceCore)(object)sender!).Callback;
        var call = new CallFunction<T>(function, callback, false);
        context.QueueNext(new CommandQueueEntry<T>(
            CreateTopFrame(context, functionReturn), call.ToUnboundAction().Bind(sender)));
    }

    //QueueInitialCommandExecution enqueues the first command; maps to vanilla queueInitialCommandExecution
    public static void QueueInitialCommandExecution(ExecutionContext<T> context, string command,
        ContextChain<T> executionChain, T sender, CommandResultCallback commandReturn)
    {
        var topLevel = new BuildContexts<T>.TopLevel(command, executionChain, sender);
        context.QueueNext(new CommandQueueEntry<T>(
            CreateTopFrame(context, commandReturn), topLevel.Execute));
    }

    private void HandleQueueOverflow()
    {
        _queueOverflow = true;
        _newTopCommands.Clear();
        _commandQueue.Clear();
    }

    //QueueNext appends an action at the front side, consumed depth-first
    public void QueueNext(CommandQueueEntry<T> entry)
    {
        if (_newTopCommands.Count + _commandQueue.Count > MaxQueueDepth)
        {
            HandleQueueOverflow();
        }
        if (!_queueOverflow)
        {
            _newTopCommands.Add(entry);
        }
    }

    //DiscardAtDepthOrHigher discards queue entries no shallower than the given depth; maps to vanilla discardAtDepthOrHigher
    public void DiscardAtDepthOrHigher(int depthToDiscard)
    {
        while (_commandQueue.Count > 0 && _commandQueue.First!.Value.Frame.Depth >= depthToDiscard)
        {
            _commandQueue.RemoveFirst();
        }
    }

    //FrameControlForDepth gets the frame discard callback for a depth
    public FrameControl FrameControlForDepth(int depthToDiscard)
        => () => DiscardAtDepthOrHigher(depthToDiscard);

    //RunCommandQueue consumes the whole queue, breaking on quota exhaustion or overflow; maps to vanilla runCommandQueue
    public void RunCommandQueue()
    {
        PushNewCommands();
        while (true)
        {
            if (_commandQuota <= 0)
            {
                Log.Info($"Command execution stopped due to limit (executed {_commandLimit} commands)");
                break;
            }
            var command = _commandQueue.First;
            if (command is null)
            {
                return;
            }
            _commandQueue.RemoveFirst();
            _currentFrameDepth = command.Value.Frame.Depth;
            command.Value.Execute(this);
            if (_queueOverflow)
            {
                Log.Error($"Command execution stopped due to command queue overflow (max {MaxQueueDepth})");
                break;
            }
            PushNewCommands();
        }
        _currentFrameDepth = 0;
    }

    private void PushNewCommands()
    {
        for (var i = _newTopCommands.Count - 1; i >= 0; --i)
        {
            _commandQueue.AddFirst(_newTopCommands[i]);
        }
        _newTopCommands.Clear();
    }

    //Tracer read/write the execution tracer
    public void Tracer(TraceCallbacks? tracer) => _tracer = tracer;

    public TraceCallbacks? Tracer() => _tracer;

    //Profiler profiler entry point
    public ProfilerFiller Profiler() => _profiler;

    //ForkLimit fork limit
    public int ForkLimit() => _forkLimit;

    //IncrementCost consumes one command quota
    public void IncrementCost() => --_commandQuota;

    //Dispose disposes the tracer
    public void Dispose()
    {
        _tracer?.Dispose();
    }
}
