namespace NetCraft.Commands.Execution;

//UnboundEntryAction 未绑定发送者的动作对应原版 net.minecraft.commands.execution.UnboundEntryAction
//函数条目编译时不知道执行者 入队时才 Bind
//不加 in T 变型 ExecutionContext<T> 不变式 与 T 一起固定最省心
public delegate void UnboundEntryAction<T>(T sender, ExecutionContext<T> context, Frame frame);

//UnboundEntryActionExtensions 委托扩展承载原版 bind 默认方法
public static class UnboundEntryActionExtensions
{
    //Bind 绑定发送者变成队列动作
    public static EntryAction<T> Bind<T>(this UnboundEntryAction<T> action, T sender)
        => (context, frame) => action(sender, context, frame);
}
