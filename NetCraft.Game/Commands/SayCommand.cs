using NetCraft.Commands;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Network.Chat;

namespace NetCraft.Game.Commands;

//SayCommand say 命令对应原版 net.minecraft.server.commands.SayCommand
//把执行者说的话按 chat.type.announcement 装饰后广播给全服
public static class SayCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("say")
            .Requires(s => s.HasPermission(2))
            .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("message", MessageArgument.Message())
                .Executes(context =>
                {
                    var source = (ServerCommandSource)context.GetSource();
                    //原版走 broadcastChatMessage 配 ChatType.SAY_COMMAND 的装饰就是 [发送者] 内容
                    source.Server.PlayerList.BroadcastSystemMessage(
                        Component.Translatable("chat.type.announcement",
                            Component.Literal(source.SenderName),
                            Component.Literal(MessageArgument.GetMessage(context, "message"))),
                        false);
                    return 1;
                })));
    }
}
