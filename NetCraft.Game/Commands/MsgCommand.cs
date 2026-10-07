using NetCraft.Commands;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Network.Chat;

namespace NetCraft.Game.Commands;

//MsgCommand msg/tell/w command, maps to vanilla net.minecraft.server.commands.MsgCommand
//The private message is sent only to the target and the sender, not broadcast to others
public static class MsgCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        //The three literals share one subtree, consistent with vanilla registering the msg/tell/w entries
        foreach (var name in new[] { "msg", "tell", "w" })
            dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal(name)
                .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("targets", EntityArgument.Players())
                    .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("message", MessageArgument.Message())
                        .Executes(Send))));
    }

    //Send delivers per target and gives the sender a summary reply
    private static int Send(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var targets = EntityArgument.GetPlayers(context, "targets");
        if (targets.Count == 0)
        {
            source.SendFailure("no matching player found");
            return 0;
        }

        var senderName = source.SenderName;
        var message = MessageArgument.GetMessage(context, "message");
        var names = new List<string>(targets.Count);
        foreach (var target in targets)
        {
            names.Add(target.Profile.Name);
            target.SendSystemMessage(Component.Literal($"{senderName} -> you: {message}"));
        }

        source.SendSuccess($"you -> {string.Join(", ", names)}: {message}");
        return targets.Count;
    }
}
