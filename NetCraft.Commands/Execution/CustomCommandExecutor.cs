using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;

namespace NetCraft.Commands.Execution;

//CustomCommandExecutor 自定义命令执行器对应原版 net.minecraft.commands.execution.CustomCommandExecutor
//拿到完整链与执行控制 自己决定往队列里排什么 /execute 与宏 /function 落在这条路径
//原版另有 CommandAdapter 桥到 brigadier Command C# 命令是委托 桥接交给注册侧的闭包
public interface CustomCommandExecutor<T>
{
    //NotExecutable 自定义执行器注册时命令槽里的占位委托 真正执行走 Run
    public static readonly Command<T> NotExecutable = _ => throw new NotSupportedException("This function should not run");

    //Run 执行自定义逻辑
    void Run(T sender, ContextChain<T> currentStep, ChainModifiers modifiers, ExecutionControl<T> output);

    //WithErrorHandling 带异常兜底的骨架对应原版 WithErrorHandling
    //约束到非泛型核心接口 T 在真实调用侧都实现它
    public abstract class WithErrorHandling<T2> : CustomCommandExecutor<T2> where T2 : ExecutionSourceCore
    {
        public void Run(T2 sender, ContextChain<T2> currentStep, ChainModifiers modifiers, ExecutionControl<T2> output)
        {
            try
            {
                RunGuarded(sender, currentStep, modifiers, output);
            }
            catch (CommandSyntaxException e)
            {
                OnError(e, sender, modifiers, output.Tracer());
                sender.Callback.OnFailure();
            }
        }

        //OnError 错误上报默认走源的 HandleError
        protected virtual void OnError(CommandSyntaxException e, T2 sender, ChainModifiers modifiers, TraceCallbacks? tracer)
            => sender.HandleError(e.Type, e.RawMessage, modifiers.IsForked(), tracer);

        //RunGuarded 子类真正的执行体
        protected abstract void RunGuarded(T2 sender, ContextChain<T2> currentStep, ChainModifiers modifiers, ExecutionControl<T2> output);
    }
}
