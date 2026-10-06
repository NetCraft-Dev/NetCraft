namespace NetCraft.Commands.Execution.Tasks;

//FallthroughTask 空源集合的返回收尾对应原版 net.minecraft.commands.execution.tasks.FallthroughTask
//return 模式下修饰后没有源了 回一次失败并废弃本帧
public class FallthroughTask<T>
{
    private static readonly EntryAction<T> _instance = Execute;

    //Instance 单例动作
    public static EntryAction<T> Instance() => _instance;

    private static void Execute(ExecutionContext<T> context, Frame frame)
    {
        frame.ReturnFailure();
        frame.Discard();
    }
}
