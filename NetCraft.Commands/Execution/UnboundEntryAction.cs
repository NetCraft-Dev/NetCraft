namespace NetCraft.Commands.Execution;

//UnboundEntryAction action with an unbound sender, maps to vanilla net.minecraft.commands.execution.UnboundEntryAction
//A function entry does not know its executor at compile time; it is bound at enqueue time
//No in T variance: ExecutionContext<T> is invariant, so fixing it together with T is simplest
public delegate void UnboundEntryAction<T>(T sender, ExecutionContext<T> context, Frame frame);

//UnboundEntryActionExtensions delegate extensions carrying the vanilla bind default method
public static class UnboundEntryActionExtensions
{
    //Bind binds the sender, turning it into a queue action
    public static EntryAction<T> Bind<T>(this UnboundEntryAction<T> action, T sender)
        => (context, frame) => action(sender, context, frame);
}
