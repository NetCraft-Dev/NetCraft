using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;

namespace NetCraft.Commands.Execution.Tasks;

//ExecuteCommand 普通命令的最终执行动作对应原版 net.minecraft.commands.execution.tasks.ExecuteCommand
//BUILD_EXECUTOR 段之后真正调用命令委托的地方
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
            //结果同时喂给源回调 return 模式再喂给帧的返回消费者
            //等价原版把帧消费者链进源回调的做法 免掉引擎侧 WithCallback
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

    //Bind 绑定发送者成队列动作
    public EntryAction<T> Bind(T sender)
        => (context, frame) => Execute(sender, context, frame);
}
