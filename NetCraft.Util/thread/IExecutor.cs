namespace NetCraft.Util.Thread;

//执行器抽象对应原版java.util.concurrent.Executor
//提交Action到具体执行线程池由实现决定
public interface IExecutor
{
    string Name { get; }
    void Execute(Action task);
}
