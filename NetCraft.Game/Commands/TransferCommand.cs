using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Network.Protocol.Common;
using NetCraft.Game.Server;

namespace NetCraft.Game.Commands;

//TransferCommand transfer command, maps to vanilla net.minecraft.server.commands.TransferCommand
//Transfers the target players to another server; without players it transfers only yourself; without a port it uses vanilla's default 25565
public static class TransferCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("transfer")
            .Requires(s => s.HasPermission(3))
            .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("hostname", StringArgumentType.String())
                .Executes(context => Transfer(context, 25565, null))
                .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("port", IntegerArgumentType.Integer(1, 65535))
                    .Executes(context => Transfer(context, IntegerArgumentType.GetInteger(context, "port"), null))
                    .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("players", EntityArgument.Players())
                        .Executes(context => Transfer(context, IntegerArgumentType.GetInteger(context, "port"),
                            EntityArgument.GetPlayers(context, "players")))))));
    }

    //Transfer sends the transfer packet per target; without a port it uses 25565
    private static int Transfer(CommandContext<CommandSourceStack> context, int port, IReadOnlyList<ServerPlayer>? explicitTargets)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var host = StringArgumentType.GetString(context, "hostname");
        var targets = explicitTargets ?? new[] { source.Player };
        if (targets.Count == 0)
        {
            source.SendFailure("no matching player found");
            return 0;
        }

        foreach (var target in targets)
            target.Connection.Send(new ClientboundTransferPacket(host, port));

        source.SendSuccess(targets.Count == 1
            ? $"transferred {targets[0].Profile.Name} to {host}:{port}"
            : $"transferred {targets.Count} players to {host}:{port}");
        return targets.Count;
    }
}
