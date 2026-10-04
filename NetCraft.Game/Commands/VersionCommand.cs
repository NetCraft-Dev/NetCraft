using NetCraft.Commands;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;

namespace NetCraft.Game.Commands;

//VersionCommand version 命令对应原版 net.minecraft.server.commands.VersionCommand
//回执服务端程序集版本号
public static class VersionCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("version")
            .Executes(context =>
            {
                var source = (ServerCommandSource)context.GetSource();
                var version = typeof(VersionCommand).Assembly.GetName().Version?.ToString() ?? "未知";
                source.SendSuccess($"此服务器运行 NetCraft {version}");
                return 1;
            }));
    }
}
