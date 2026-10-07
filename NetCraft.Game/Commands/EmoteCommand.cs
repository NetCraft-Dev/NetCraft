using NetCraft.Commands;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Network.Chat;

namespace NetCraft.Game.Commands;

//EmoteCommand me command, maps to vanilla net.minecraft.server.commands.EmoteCommands
//Decorates the executor's action with chat.type.emote and broadcasts it to the whole server; no permission required
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
