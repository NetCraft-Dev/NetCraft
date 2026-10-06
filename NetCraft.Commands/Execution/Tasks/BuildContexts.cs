using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Registry;

namespace NetCraft.Commands.Execution.Tasks;

//BuildContexts 把解析好的上下文链逐段展开成可执行命令对应原版 net.minecraft.commands.execution.tasks.BuildContexts
//修饰器阶段逐级换源 fork 修饰翻标记 遇自定义修饰器/执行器把控制权交出去
//到 EXECUTE 段后普通命令排 ExecuteCommand 动作由 ContinuationTask 逐源续跑
public class BuildContexts<T>
{
    //ErrorForkLimitReached 分叉超限
    public static readonly DynamicCommandExceptionType ErrorForkLimitReached =
        new(limit => new LiteralMessage($"Command fork limit of {limit} has been reached"));

    private readonly string _commandInput;
    private readonly ContextChain<T> _command;

    public BuildContexts(string commandInput, ContextChain<T> command)
    {
        _commandInput = commandInput;
        _command = command;
    }

    //Execute 逐段展开修饰器到 EXECUTE 再派发
    protected void Execute(T originalSource, List<T> initialSources, ExecutionContext<T> context, Frame frame,
        ChainModifiers initialModifiers)
    {
        var currentStage = _command;
        var modifiers = initialModifiers;
        var currentSources = initialSources;
        if (currentStage.GetStage() != ContextChain<T>.Stage.Execute)
        {
            context.Profiler().Push(() => "prepare " + _commandInput);
            try
            {
                var forkLimit = context.ForkLimit();
                while (currentStage.GetStage() != ContextChain<T>.Stage.Execute)
                {
                    var contextToRun = currentStage.GetTopContext();
                    if (contextToRun.IsForked())
                    {
                        modifiers = modifiers.SetForked();
                    }
                    var modifier = contextToRun.GetRedirectModifier();
                    if (modifier is CustomModifierExecutor<T> customModifierExecutor)
                    {
                        customModifierExecutor.Apply(originalSource, currentSources, currentStage, modifiers,
                            ExecutionControl<T>.Create(context, frame));
                        return;
                    }
                    if (modifier is not null)
                    {
                        context.IncrementCost();
                        var forkedMode = modifiers.IsForked();
                        var nextSources = new List<T>();
                        foreach (var source in currentSources)
                        {
                            ICollection<T> newSources;
                            try
                            {
                                newSources = ContextChain<T>.RunModifier(contextToRun, source,
                                    (_, _, _) => { }, forkedMode);
                                if (nextSources.Count + newSources.Count >= forkLimit)
                                {
                                    AsCore(originalSource).HandleError(ErrorForkLimitReached.Create(forkLimit),
                                        forkedMode, context.Tracer());
                                    return;
                                }
                            }
                            catch (CommandSyntaxException e)
                            {
                                AsCore(source).HandleError(e.Type, e.RawMessage, forkedMode, context.Tracer());
                                if (forkedMode) continue;
                                context.Profiler().Pop();
                                return;
                            }
                            nextSources.AddRange(newSources);
                        }
                        currentSources = nextSources;
                    }
                    currentStage = currentStage.NextStage()
                        ?? throw new InvalidOperationException("Context chain ended before EXECUTE stage");
                }
            }
            finally
            {
                context.Profiler().Pop();
            }
        }
        if (currentSources.Count == 0)
        {
            if (modifiers.IsReturn())
            {
                context.QueueNext(new CommandQueueEntry<T>(frame, FallthroughTask<T>.Instance()));
            }
            return;
        }
        var executeContext = currentStage.GetTopContext();
        if (executeContext.CustomExecutor is { } customCommandExecutor)
        {
            var executionControl = ExecutionControl<T>.Create(context, frame);
            foreach (var source in currentSources)
            {
                customCommandExecutor.Run(source, currentStage, modifiers, executionControl);
            }
        }
        else
        {
            var action = new ExecuteCommand<T>(_commandInput, modifiers, executeContext);
            ContinuationTask<T, T>.Schedule(context, frame, currentSources,
                (frame1, entrySource) => new CommandQueueEntry<T>(frame1, action.Bind(entrySource)));
        }
    }

    //AsCore 执行引擎无约束泛型下访问源核心成员的显式转换
    //引擎与 brigadier 的无约束 S 共存 T 在真实调用侧都是实现 ExecutionSourceCore 的源类型
    private static ExecutionSourceCore AsCore(T source)
        => (ExecutionSourceCore)(object)source!;

    //TraceCommandStart 命令开始追踪
    protected void TraceCommandStart(ExecutionContext<T> context, Frame frame)
    {
        if (context.Tracer() is { } tracer)
        {
            tracer.OnCommand(frame.Depth, _commandInput);
        }
    }

    public override string ToString() => _commandInput;

    //TopLevel 顶层入口 一条玩家或控制台命令的起点
    public sealed class TopLevel(string commandInput, ContextChain<T> command, T source)
        : BuildContexts<T>(commandInput, command)
    {
        public void Execute(ExecutionContext<T> context, Frame frame)
        {
            TraceCommandStart(context, frame);
            Execute(source, [source], context, frame, ChainModifiers.Default);
        }
    }

    //Continuation 修饰器续跑入口 源集合已展开
    public sealed class Continuation(string commandInput, ContextChain<T> command, ChainModifiers modifiers,
        T originalSource, List<T> sources)
        : BuildContexts<T>(commandInput, command)
    {
        public void Execute(ExecutionContext<T> context, Frame frame)
        {
            Execute(originalSource, sources, context, frame, modifiers);
        }
    }

    //Unbound 函数条目用的未绑定入口
    public sealed class Unbound(string commandInput, ContextChain<T> command)
        : BuildContexts<T>(commandInput, command)
    {
        public void Execute(T sender, ExecutionContext<T> context, Frame frame)
        {
            TraceCommandStart(context, frame);
            Execute(sender, [sender], context, frame, ChainModifiers.Default);
        }

        //Bind 包装成未绑定动作委托 对应原版 Unbound 实现 UnboundEntryAction
        public UnboundEntryAction<T> ToUnboundAction()
            => (sender, context, frame) => Execute(sender, context, frame);
    }
}
