using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Server;

namespace NetCraft.Game.Commands;

//KillCommand kill command, maps to vanilla net.minecraft.server.commands.KillCommand
//kill kills the executor; kill <targets> kills the target set; permission 2
public static class KillCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("kill")
            .Requires(s => s.HasPermission(2))
            .Executes(context => Kill(context, SelfTargets(context)))
            .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("targets", EntityArgument.Entities())
                .Executes(context => Kill(context, EntityArgument.GetEntities(context, "targets")))));
    }

    //SelfTargets applies to the executor when targets is omitted, maps to vanilla getEntityOrException
    private static IReadOnlyList<CommandTarget> SelfTargets(CommandContext<CommandSourceStack> context)
        => context.GetSource() is ServerCommandSource source
            ? new[] { CommandTarget.OfPlayer(source.PlayerOrThrow) }
            : Array.Empty<CommandTarget>();

    //Kill applies lethal damage to each target and reports, maps to vanilla kill
    private static int Kill(CommandContext<CommandSourceStack> context, IReadOnlyList<CommandTarget> victims)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        foreach (var victim in victims) KillTarget(source, victim);
        source.SendSuccess(victims.Count == 1
            ? $"killed {victims[0].Name}"
            : $"killed {victims.Count} entities");
        return victims.Count;
    }

    //KillTarget players go through PlayerList's damage chain; on zero health it broadcasts the death message and resets
    //Level entities are dealt lethal damage directly; the death callback removes them from the world and broadcasts the death event
    private static void KillTarget(ServerCommandSource source, CommandTarget target)
    {
        if (target.Player is { } player)
        {
            source.Server.PlayerList.HurtPlayer(player, null, float.MaxValue);
            return;
        }
        target.WorldEntity?.Hurt(float.MaxValue);
    }
}
