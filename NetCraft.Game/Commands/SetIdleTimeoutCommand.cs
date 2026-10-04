using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;

namespace NetCraft.Game.Commands;

//SetIdleTimeoutCommand setidletimeout 命令对应原版 net.minecraft.server.commands.SetIdleTimeoutCommand
//挂机踢出分钟数 0 表示不踢 写回 server.properties
public static class SetIdleTimeoutCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("setidletimeout")
            .Requires(s => s.HasPermission(3))
            .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("minutes", IntegerArgumentType.Integer(0))
                .Executes(Set)));
    }

    //Set 改写挂机超时并落盘 0 关闭该功能
    private static int Set(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var minutes = IntegerArgumentType.GetInteger(context, "minutes");
        source.Server.Settings.SetPlayerIdleTimeout(minutes);
        source.Server.Settings.SaveCurrent();
        source.SendSuccess(minutes == 0
            ? "已关闭挂机踢出"
            : $"挂机踢出已设为 {minutes} 分钟");
        return minutes;
    }
}
