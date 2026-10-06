namespace NetCraft.Commands.Execution;

//CommandQueueEntry 执行队列条目对应原版 net.minecraft.commands.execution.CommandQueueEntry
//帧与动作成对入队 动作在帧内执行
public sealed record CommandQueueEntry<T>(Frame Frame, EntryAction<T> Action)
{
    //Execute 在执行上下文里跑这条动作
    public void Execute(ExecutionContext<T> context) => Action(context, Frame);
}
