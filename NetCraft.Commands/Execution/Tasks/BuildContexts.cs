using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Registry;

namespace NetCraft.Commands.Execution.Tasks;

//BuildContexts expands the parsed context chain stage by stage into an executable command; maps to vanilla net.minecraft.commands.execution.tasks.BuildContexts
//The modifier stage swaps sources level by level, fork modifiers flip the flag, and a custom modifier/executor hands control over
//Once at the EXECUTE stage, an ordinary command queues an ExecuteCommand action and ContinuationTask continues source by source
public class BuildContexts<T>
{
    //ErrorForkLimitReached fork limit exceeded
    public static readonly DynamicCommandExceptionType ErrorForkLimitReached =
        new(limit => new LiteralMessage($"Command fork limit of {limit} has been reached"));

    private readonly string _commandInput;
    private readonly ContextChain<T> _command;

    public BuildContexts(string commandInput, ContextChain<T> command)
    {
        _commandInput = commandInput;
        _command = command;
    }

    //Execute expands modifiers stage by stage down to EXECUTE, then dispatches
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

    //AsCore explicit cast used to access the source's core members under the engine's unconstrained generic
    //The engine coexists with brigadier's unconstrained S; on the real call side T is always a source type implementing ExecutionSourceCore
    private static ExecutionSourceCore AsCore(T source)
        => (ExecutionSourceCore)(object)source!;

    //TraceCommandStart trace command start
    protected void TraceCommandStart(ExecutionContext<T> context, Frame frame)
    {
        if (context.Tracer() is { } tracer)
        {
            tracer.OnCommand(frame.Depth, _commandInput);
        }
    }

    public override string ToString() => _commandInput;

    //TopLevel top-level entry, the starting point of a player or console command
    public sealed class TopLevel(string commandInput, ContextChain<T> command, T source)
        : BuildContexts<T>(commandInput, command)
    {
        public void Execute(ExecutionContext<T> context, Frame frame)
        {
            TraceCommandStart(context, frame);
            Execute(source, [source], context, frame, ChainModifiers.Default);
        }
    }

    //Continuation continuation entry after the modifier stage; the source collection is already expanded
    public sealed class Continuation(string commandInput, ContextChain<T> command, ChainModifiers modifiers,
        T originalSource, List<T> sources)
        : BuildContexts<T>(commandInput, command)
    {
        public void Execute(ExecutionContext<T> context, Frame frame)
        {
            Execute(originalSource, sources, context, frame, modifiers);
        }
    }

    //Unbound unbound entry used by function entries
    public sealed class Unbound(string commandInput, ContextChain<T> command)
        : BuildContexts<T>(commandInput, command)
    {
        public void Execute(T sender, ExecutionContext<T> context, Frame frame)
        {
            TraceCommandStart(context, frame);
            Execute(sender, [sender], context, frame, ChainModifiers.Default);
        }

        //Bind wraps it into an unbound action delegate, mirroring how vanilla Unbound implements UnboundEntryAction
        public UnboundEntryAction<T> ToUnboundAction()
            => (sender, context, frame) => Execute(sender, context, frame);
    }
}
