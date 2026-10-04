using NetCraft.Commands;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Network.Chat;

namespace NetCraft.Game.Commands;

//EmoteCommand me 命令对应原版 net.minecraft.server.commands.EmoteCommands
//把执行者的动作按 chat.type.emote 装饰后广播给全服 无需权限
public static class EmoteCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("me")
            .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("action", MessageArgument.Message())
                .Executes(context =>
                {
                    var source = (ServerCommandSource)context.GetSource();
                    source.Server.PlayerList.BroadcastSystemMessage(
                        Component.Translatable("chat.type.emote",
                            Component.Literal(source.SenderName),
                            Component.Literal(MessageArgument.GetMessage(context, "action"))),
                        false);
                    return 1;
                })));
    }
}
