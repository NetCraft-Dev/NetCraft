using NetCraft.Commands;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Network.Protocol.Game;

namespace NetCraft.Game.Commands;

//SwingCommand swing command, maps to vanilla net.minecraft.server.commands.SwingCommand
//Makes the targets play the swing animation; the action id matches vanilla ClientboundAnimatePacket; 0 main hand, 3 offhand
public static class SwingCommand
{
    private const int SwingMainHand = 0;
    private const int SwingOffHand = 3;

    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("swing")
            .Requires(s => s.HasPermission(2))
            .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("targets", EntityArgument.Players())
                .Executes(context => Swing(context, SwingMainHand))
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("mainhand")
                    .Executes(context => Swing(context, SwingMainHand)))
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("offhand")
                    .Executes(context => Swing(context, SwingOffHand)))));
    }

    //Swing broadcasts the animation packet to the whole server, maps to vanilla broadcastAnimate
    private static int Swing(CommandContext<CommandSourceStack> context, int action)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var targets = EntityArgument.GetPlayers(context, "targets");
        if (targets.Count == 0)
        {
            source.SendFailure("no matching player found");
            return 0;
        }

        foreach (var target in targets)
            source.Server.PlayerList.BroadcastAll(new ClientboundAnimatePacket(target.EntityId, action));

        source.SendSuccess($"made {targets.Count} players swing");
        return targets.Count;
    }
}
