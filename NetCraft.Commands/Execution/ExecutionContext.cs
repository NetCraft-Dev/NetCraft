using NetCraft.Commands.Context;
using NetCraft.Commands.Functions;
using NetCraft.Commands.Execution.Tasks;
using NetCraft.Logging;
using NetCraft.Util.Profiling;

namespace NetCraft.Commands.Execution;

//ExecutionContext 命令执行上下文对应原版 net.minecraft.commands.execution.ExecutionContext
//命令按队列逐条跑 每条消耗配额 新入队命令插到队首实现深度优先
//队列超限与配额耗尽都有熔断 阈值对齐原版 10000000
public class ExecutionContext<T> : IDisposable
{
    //MaxQueueDepth 队列深度熔断阈值
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

    //CreateTopFrame 顶层帧 队列已在顶层时废弃即清空整个队列 对应原版 createTopFrame
    private static Frame CreateTopFrame(ExecutionContext<T> context, CommandResultCallback frameResult)
    {
        if (context._currentFrameDepth == 0)
        {
            return new Frame(0, frameResult, context._commandQueue.Clear);
        }
        var reentrantFrameDepth = context._currentFrameDepth + 1;
        return new Frame(reentrantFrameDepth, frameResult, context.FrameControlForDepth(reentrantFrameDepth));
    }

    //QueueInitialFunctionCall 排入首条函数调用对应原版 queueInitialFunctionCall
    public static void QueueInitialFunctionCall(ExecutionContext<T> context, InstantiatedFunction<T> function,
        T sender, CommandResultCallback functionReturn)
    {
        var callback = ((ExecutionSourceCore)(object)sender!).Callback;
        var call = new CallFunction<T>(function, callback, false);
        context.QueueNext(new CommandQueueEntry<T>(
            CreateTopFrame(context, functionReturn), call.ToUnboundAction().Bind(sender)));
    }

    //QueueInitialCommandExecution 排入首条命令对应原版 queueInitialCommandExecution
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

    //QueueNext 追加一条动作插在队首一侧 深度优先消费
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

    //DiscardAtDepthOrHigher 废弃不浅于指定深度的队列项对应原版 discardAtDepthOrHigher
    public void DiscardAtDepthOrHigher(int depthToDiscard)
    {
        while (_commandQueue.Count > 0 && _commandQueue.First!.Value.Frame.Depth >= depthToDiscard)
        {
            _commandQueue.RemoveFirst();
        }
    }

    //FrameControlForDepth 按深度取帧废弃回调
    public FrameControl FrameControlForDepth(int depthToDiscard)
        => () => DiscardAtDepthOrHigher(depthToDiscard);

    //RunCommandQueue 消费整个队列配额耗尽或超限熔断对应原版 runCommandQueue
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

    //Tracer 读写执行追踪器
    public void Tracer(TraceCallbacks? tracer) => _tracer = tracer;

    public TraceCallbacks? Tracer() => _tracer;

    //Profiler 性能分析入口
    public ProfilerFiller Profiler() => _profiler;

    //ForkLimit 分叉上限
    public int ForkLimit() => _forkLimit;

    //IncrementCost 消耗一条命令配额
    public void IncrementCost() => --_commandQuota;

    //Dispose 收尾追踪器
    public void Dispose()
    {
        _tracer?.Dispose();
    }
}
