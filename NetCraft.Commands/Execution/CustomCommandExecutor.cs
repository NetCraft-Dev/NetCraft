using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;

namespace NetCraft.Commands.Execution;

//CustomCommandExecutor custom command executor, maps to vanilla net.minecraft.commands.execution.CustomCommandExecutor
//Gets the full chain and execution control and decides for itself what to enqueue; /execute and macro /function go down this path
//Vanilla also has CommandAdapter bridging to a brigadier Command; in C# a command is a delegate, so the bridge is left to the registration-side closure
public interface CustomCommandExecutor<T>
{
    //NotExecutable placeholder delegate in the command slot when a custom executor is registered; real execution goes through Run
    public static readonly Command<T> NotExecutable = _ => throw new NotSupportedException("This function should not run");

    //Run runs the custom logic
    void Run(T sender, ContextChain<T> currentStep, ChainModifiers modifiers, ExecutionControl<T> output);

    //WithErrorHandling skeleton with error fallback, maps to vanilla WithErrorHandling
    //Constrained to the non-generic core interface, which T always implements on the real call side
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

        //OnError error reporting defaults to the source's HandleError
        protected virtual void OnError(CommandSyntaxException e, T2 sender, ChainModifiers modifiers, TraceCallbacks? tracer)
            => sender.HandleError(e.Type, e.RawMessage, modifiers.IsForked(), tracer);

        //RunGuarded the actual execution body of the subclass
        protected abstract void RunGuarded(T2 sender, ContextChain<T2> currentStep, ChainModifiers modifiers, ExecutionControl<T2> output);
    }
}
