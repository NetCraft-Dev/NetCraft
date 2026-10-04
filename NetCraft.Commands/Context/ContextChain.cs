using NetCraft.Codec;
using NetCraft.Commands.Exceptions;

namespace NetCraft.Commands.Context;

//ContextChain 上下文链对应原版com.mojang.brigadier.context.ContextChain
//将CommandContext链式结构展平为modifier列表与末尾executable
//executeAll跨modifier链累积forkedMode逐级expand源列表
public sealed class ContextChain<S>
{
    private readonly List<CommandContext<S>> _modifiers;
    private readonly CommandContext<S> _executable;
    private ContextChain<S>? _nextStageCache;

    public ContextChain(List<CommandContext<S>> modifiers, CommandContext<S> executable)
    {
        if (executable.GetCommand() == null)
        {
            throw new ArgumentException("Last command in chain must be executable");
        }
        _modifiers = modifiers;
        _executable = executable;
    }

    public static Optional<ContextChain<S>> TryFlatten(CommandContext<S> rootContext)
    {
        var modifiers = new List<CommandContext<S>>();
        var current = rootContext;

        while (true)
        {
            var child = current.GetChild();
            if (child == null)
            {
                if (current.GetCommand() == null)
                {
                    return Optional<ContextChain<S>>.Empty();
                }
                return Optional<ContextChain<S>>.Of(new ContextChain<S>(modifiers, current));
            }

            modifiers.Add(current);
            current = child;
        }
    }

    //runModifier 应用RedirectModifier展开源列表forkedMode下异常返回空集合
    public static ICollection<S> RunModifier(CommandContext<S> modifier, S source, ResultConsumer<S> resultConsumer, bool forkedMode)
    {
        var sourceModifier = modifier.GetRedirectModifier();

        if (sourceModifier == null)
        {
            return new[] { source };
        }

        var contextToUse = modifier.CopyFor(source);
        try
        {
            return sourceModifier(contextToUse);
        }
        catch (CommandSyntaxException)
        {
            resultConsumer(contextToUse, false, 0);
            if (forkedMode)
            {
                return Array.Empty<S>();
            }
            throw;
        }
    }

    //runExecutable 执行命令回调结果forkedMode下返回1否则返回实际结果
    public static int RunExecutable(CommandContext<S> executable, S source, ResultConsumer<S> resultConsumer, bool forkedMode)
    {
        var contextToUse = executable.CopyFor(source);
        try
        {
            var result = executable.GetCommand()!(contextToUse);
            resultConsumer(contextToUse, true, result);
            return forkedMode ? 1 : result;
        }
        catch (CommandSyntaxException)
        {
            resultConsumer(contextToUse, false, 0);
            if (forkedMode)
            {
                return 0;
            }
            throw;
        }
    }

    public int ExecuteAll(S source, ResultConsumer<S> resultConsumer)
    {
        if (_modifiers.Count == 0)
        {
            return RunExecutable(_executable, source, resultConsumer, false);
        }

        var forkedMode = false;
        List<S> currentSources = new() { source };

        foreach (var modifier in _modifiers)
        {
            forkedMode |= modifier.IsForked();

            var nextSources = new List<S>();
            foreach (var sourceToRun in currentSources)
            {
                nextSources.AddRange(RunModifier(modifier, sourceToRun, resultConsumer, forkedMode));
            }
            if (nextSources.Count == 0)
            {
                return 0;
            }
            currentSources = nextSources;
        }

        var result = 0;
        foreach (var executionSource in currentSources)
        {
            result += RunExecutable(_executable, executionSource, resultConsumer, forkedMode);
        }

        return result;
    }

    public Stage GetStage() => _modifiers.Count == 0 ? Stage.Execute : Stage.Modify;

    public CommandContext<S> GetTopContext()
    {
        if (_modifiers.Count == 0)
        {
            return _executable;
        }
        return _modifiers[0];
    }

    public ContextChain<S>? NextStage()
    {
        var modifierCount = _modifiers.Count;
        if (modifierCount == 0)
        {
            return null;
        }

        if (_nextStageCache == null)
        {
            _nextStageCache = new ContextChain<S>(_modifiers.GetRange(1, modifierCount - 1), _executable);
        }
        return _nextStageCache;
    }

    public enum Stage
    {
        Modify,
        Execute,
    }
}
