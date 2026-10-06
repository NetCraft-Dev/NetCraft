namespace NetCraft.Commands.Execution;

//EntryAction a single action in the execution queue, maps to vanilla net.minecraft.commands.execution.EntryAction
//Vanilla uses a functional interface; C# carries it as a delegate, and class implementations (such as BuildContexts.TopLevel) enqueue via method-group conversion
public delegate void EntryAction<T>(ExecutionContext<T> context, Frame frame);
