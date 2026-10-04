using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Network.Protocol.Common;
using NetCraft.Game.Server;

namespace NetCraft.Game.Commands;

//TransferCommand transfer 命令对应原版 net.minecraft.server.commands.TransferCommand
//把目标玩家转交到另一台服务器 省略玩家时只传自己 省略端口时用原版默认 25565
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

    //Transfer 逐目标下发转交包 不给端口参数时用 25565
    private static int Transfer(CommandContext<CommandSourceStack> context, int port, IReadOnlyList<ServerPlayer>? explicitTargets)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var host = StringArgumentType.GetString(context, "hostname");
        var targets = explicitTargets ?? new[] { source.Player };
        if (targets.Count == 0)
        {
            source.SendFailure("没有找到匹配的玩家");
            return 0;
        }

        foreach (var target in targets)
            target.Connection.Send(new ClientboundTransferPacket(host, port));

        source.SendSuccess(targets.Count == 1
            ? $"已将 {targets[0].Profile.Name} 转交到 {host}:{port}"
            : $"已将 {targets.Count} 名玩家转交到 {host}:{port}");
        return targets.Count;
    }
}
