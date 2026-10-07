using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;

namespace NetCraft.Game.Commands;

//DamageCommand damage command, maps to vanilla net.minecraft.server.commands.DamageCommand
//Only keeps the core <targets> <amount>; the vanilla damage type and source branches depend on the damage source subsystem
public static class DamageCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("damage")
            .Requires(s => s.HasPermission(2))
            .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("targets", EntityArgument.Players())
                .Then(RequiredArgumentBuilder<CommandSourceStack, float>.Argument("amount", FloatArgumentType.FloatArg(0f))
                    .Executes(Apply))));
    }

    //Apply deducts health per target; dead targets or those in invulnerability frames are not counted
    private static int Apply(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var targets = EntityArgument.GetPlayers(context, "targets");
        var amount = FloatArgumentType.GetFloat(context, "amount");
        var hurt = 0;
        foreach (var target in targets)
            if (source.Server.PlayerList.HurtPlayer(target, null, amount)) hurt++;

        source.SendSuccess($"dealt {amount} damage to {hurt} players");
        return hurt;
    }
}
