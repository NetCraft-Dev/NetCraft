using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;

namespace NetCraft.Game.Commands;

//DamageCommand damage 命令对应原版 net.minecraft.server.commands.DamageCommand
//只保留最核心的 <targets> <amount> 原版的伤害类型与来源分支依赖伤害来源子系统
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

    //Apply 逐目标扣血 已死亡或处于无敌帧的目标不计入
    private static int Apply(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var targets = EntityArgument.GetPlayers(context, "targets");
        var amount = FloatArgumentType.GetFloat(context, "amount");
        var hurt = 0;
        foreach (var target in targets)
            if (source.Server.PlayerList.HurtPlayer(target, null, amount)) hurt++;

        source.SendSuccess($"已对 {hurt} 名玩家造成 {amount} 点伤害");
        return hurt;
    }
}
