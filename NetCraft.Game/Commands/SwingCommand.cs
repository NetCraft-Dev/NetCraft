using NetCraft.Commands;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Network.Protocol.Game;

namespace NetCraft.Game.Commands;

//SwingCommand swing 命令对应原版 net.minecraft.server.commands.SwingCommand
//让目标做出挥手动画 动作编号与原版 ClientboundAnimatePacket 一致 0 主手 3 副手
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

    //Swing 把动画包广播给全服 对应原版 broadcastAnimate
    private static int Swing(CommandContext<CommandSourceStack> context, int action)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var targets = EntityArgument.GetPlayers(context, "targets");
        if (targets.Count == 0)
        {
            source.SendFailure("没有找到匹配的玩家");
            return 0;
        }

        foreach (var target in targets)
            source.Server.PlayerList.BroadcastAll(new ClientboundAnimatePacket(target.EntityId, action));

        source.SendSuccess($"已让 {targets.Count} 名玩家挥手");
        return targets.Count;
    }
}
