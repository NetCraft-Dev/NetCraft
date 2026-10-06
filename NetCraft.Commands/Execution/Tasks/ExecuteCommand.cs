using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;

namespace NetCraft.Commands.Execution.Tasks;

//ExecuteCommand final execution action of an ordinary command, maps to vanilla net.minecraft.commands.execution.tasks.ExecuteCommand
//Where the command delegate is actually called after the BUILD_EXECUTOR stage
public class ExecuteCommand<T>
{
    private readonly string _commandInput;
    private readonly ChainModifiers _modifiers;
    private readonly CommandContext<T> _executionContext;

    public ExecuteCommand(string commandInput, ChainModifiers modifiers, CommandContext<T> executionContext)
    {
        _commandInput = commandInput;
        _modifiers = modifiers;
        _executionContext = executionContext;
    }

    public void Execute(T sender, ExecutionContext<T> context, Frame frame)
    {
        context.Profiler().Push(() => "execute " + _commandInput);
        try
        {
            context.IncrementCost();
            //The result feeds both the source callback and, in return mode, the frame's return consumer
            //Equivalent to vanilla chaining the frame consumer into the source callback, avoiding WithCallback on the engine side
            var frameConsumer = frame.ReturnValueConsumer;
            var isReturn = _modifiers.IsReturn();
            var result = ContextChain<T>.RunExecutable(_executionContext, sender,
                (ctx, success, result) =>
                {
                    ((ExecutionSourceCore)(object)ctx.GetSource()!).Callback.OnResult(success, result);
                    if (isReturn) frameConsumer.OnResult(success, result);
                }, _modifiers.IsForked());
            if (context.Tracer() is { } tracer)
            {
                tracer.OnReturn(frame.Depth, _commandInput, result);
            }
        }
        catch (CommandSyntaxException e)
        {
            ((ExecutionSourceCore)(object)sender!).HandleError(e.Type, e.RawMessage, _modifiers.IsForked(),
                context.Tracer());
        }
        finally
        {
            context.Profiler().Pop();
        }
    }

    //Bind binds the sender into a queue action
    public EntryAction<T> Bind(T sender)
        => (context, frame) => Execute(sender, context, frame);
}
