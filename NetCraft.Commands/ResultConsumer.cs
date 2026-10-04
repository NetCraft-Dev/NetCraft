using NetCraft.Commands.Context;

namespace NetCraft.Commands;

//ResultConsumer 结果消费者委托对应原版com.mojang.brigadier.ResultConsumer
//命令执行完成时回调记录成功失败与结果码
public delegate void ResultConsumer<S>(CommandContext<S> context, bool success, int result);
