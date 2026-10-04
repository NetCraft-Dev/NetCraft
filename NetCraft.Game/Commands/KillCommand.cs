using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Server;

namespace NetCraft.Game.Commands;

//KillCommand kill 命令对应原版 net.minecraft.server.commands.KillCommand
//kill 杀死执行者自己 kill <targets> 杀死目标集合 权限 2
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

    //SelfTargets 省略 targets 时作用于执行者自己 对应原版 getEntityOrException
    private static IReadOnlyList<CommandTarget> SelfTargets(CommandContext<CommandSourceStack> context)
        => context.GetSource() is ServerCommandSource source
            ? new[] { CommandTarget.OfPlayer(source.PlayerOrThrow) }
            : Array.Empty<CommandTarget>();

    //Kill 对每个目标施加致死伤害并回执 对应原版 kill
    private static int Kill(CommandContext<CommandSourceStack> context, IReadOnlyList<CommandTarget> victims)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        foreach (var victim in victims) KillTarget(source, victim);
        source.SendSuccess(victims.Count == 1
            ? $"已杀死 {victims[0].Name}"
            : $"已杀死 {victims.Count} 个实体");
        return victims.Count;
    }

    //KillTarget 玩家走 PlayerList 的伤害链路 血量归零后广播死亡消息并复位
    //关卡实体直接施加致死伤害 死亡回调把它移出世界并广播死亡事件
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
