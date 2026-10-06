namespace NetCraft.Util.Thread;

//Executor abstraction, maps to vanilla java.util.concurrent.Executor
//Submits an Action; the concrete execution thread pool is decided by the implementation
public interface IExecutor
{
    string Name { get; }
    void Execute(Action task);
}
