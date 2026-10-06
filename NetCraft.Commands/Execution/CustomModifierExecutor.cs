using NetCraft.Commands.Context;

namespace NetCraft.Commands.Execution;

//CustomModifierExecutor custom modifier, maps to vanilla net.minecraft.commands.execution.CustomModifierExecutor
//The modifier stage does not expand the source collection but hands the chain and control to the implementation, which decides what to continue
//Vanilla also has ModifierAdapter bridging to a RedirectModifier; in C# a modifier is a delegate, so the bridge is left to the registration-side closure
public interface CustomModifierExecutor<T>
{
    //Apply applies the modifier
    void Apply(T originalSource, List<T> sources, ContextChain<T> chain, ChainModifiers modifiers, ExecutionControl<T> output);
}
