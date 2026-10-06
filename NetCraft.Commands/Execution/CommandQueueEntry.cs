namespace NetCraft.Commands.Execution;

//CommandQueueEntry execution queue entry, maps to vanilla net.minecraft.commands.execution.CommandQueueEntry
//Frame and action are enqueued as a pair; the action runs within the frame
public sealed record CommandQueueEntry<T>(Frame Frame, EntryAction<T> Action)
{
    //Execute runs this action in the execution context
    public void Execute(ExecutionContext<T> context) => Action(context, Frame);
}
