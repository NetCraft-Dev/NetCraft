namespace NetCraft.Commands.Execution;

//EntryAction 执行队列的单条动作对应原版 net.minecraft.commands.execution.EntryAction
//原版是函数式接口 C# 用委托承载 类实现(如 BuildContexts.TopLevel)以方法组转委托入队
public delegate void EntryAction<T>(ExecutionContext<T> context, Frame frame);
