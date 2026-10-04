using NetCraft.Commands.Context;

namespace NetCraft.Commands;

//Command 命令回调委托对应原版com.mojang.brigadier.Command
//在CommandContext上执行返回int结果码SingleSuccess表示成功
public delegate int Command<S>(CommandContext<S> context);

//CommandConstants 持SingleSuccess常量因C# delegate不能持常量
public static class CommandConstants
{
    public const int SingleSuccess = 1;
}
