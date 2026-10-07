using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Server;
using NetCraft.Logging;
using NetCraft.Network.Chat;

namespace NetCraft.Game.Commands;

//KickCommand kick command, maps to vanilla net.minecraft.server.commands.KickCommand
//Targets only resolve online player names; this project has no offline player profile lookup (same limitation as /op)
//Without a reason it uses vanilla multiplayer.disconnect.kicked; the client shows it in its local language
public static class KickCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("kick")
            .Requires(s => s.HasPermission(3))
            .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("targets", StringArgumentType.Word())
                .Executes(context => Kick(context, null))
                .Then(RequiredArgumentBuilder<CommandSourceStack, string>
                    .Argument("reason", StringArgumentType.GreedyString())
                    .Executes(context => Kick(context, StringArgumentType.GetString(context, "reason"))))));
    }

    //Kick disconnects the target connection, maps to vanilla kickPlayers
    private static int Kick(CommandContext<CommandSourceStack> context, string? reason)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var name = StringArgumentType.GetString(context, "targets");
        var target = source.Server.PlayerList.GetPlayerByName(name);
        if (target is null)
        {
            source.SendFailure($"player {name} is not online");
            return 0;
        }
        target.Disconnect(reason is null
            ? Component.Translatable("multiplayer.disconnect.kicked")
            : Component.Literal(reason));
        //The operator may be the console; PlayerOrThrow cannot be used here or its exception takes the later success reply with it
        Log.Info($"Kicked player {target.Profile.Name} operator={source.Player?.Profile.Name ?? "Server"} reason={reason ?? "none"}");
        source.SendSuccess(reason is null
            ? $"kicked {target.Profile.Name} from the server"
            : $"kicked {target.Profile.Name} from the server; reason: {reason}");
        return 1;
    }
}
