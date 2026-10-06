namespace NetCraft.Commands.Execution.Tasks;

//FallthroughTask return cleanup for an empty source collection, maps to vanilla net.minecraft.commands.execution.tasks.FallthroughTask
//In return mode, after modifiers leave no sources, report failure once and discard this frame
public class FallthroughTask<T>
{
    private static readonly EntryAction<T> _instance = Execute;

    //Instance singleton action
    public static EntryAction<T> Instance() => _instance;

    private static void Execute(ExecutionContext<T> context, Frame frame)
    {
        frame.ReturnFailure();
        frame.Discard();
    }
}
