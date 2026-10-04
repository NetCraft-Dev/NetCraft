using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;

namespace NetCraft.Game.Commands;

//TickCommand tick 命令对应原版 net.minecraft.server.commands.TickCommand
//query 看当前刻状态 rate 改速率 freeze/unfreeze 冻结世界 step/sprint 单步与冲刺
public static class TickCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        var query = LiteralArgumentBuilder<CommandSourceStack>.Literal("query")
            .Executes(Query);

        var rate = LiteralArgumentBuilder<CommandSourceStack>.Literal("rate")
            .Then(RequiredArgumentBuilder<CommandSourceStack, float>.Argument("rate", FloatArgumentType.FloatArg(1f, 10000f))
                .Executes(SetRate));

        //单步与冲刺的停止挂在自己下面 对应原版 /tick step stop 与 /tick sprint stop
        var step = LiteralArgumentBuilder<CommandSourceStack>.Literal("step")
            .Executes(context => Step(context, 1))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("stop")
                .Executes(StopStep))
            .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("time", TimeArgument.Time(1))
                .Executes(context => Step(context, IntegerArgumentType.GetInteger(context, "time"))));

        var sprint = LiteralArgumentBuilder<CommandSourceStack>.Literal("sprint")
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("stop")
                .Executes(StopSprint))
            .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("time", TimeArgument.Time(1))
                .Executes(context => Sprint(context, IntegerArgumentType.GetInteger(context, "time"))));

        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("tick")
            .Requires(s => s.HasPermission(3))
            .Then(query)
            .Then(rate)
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("freeze")
                .Executes(context => SetFrozen(context, true)))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("unfreeze")
                .Executes(context => SetFrozen(context, false)))
            .Then(step)
            .Then(sprint));
    }

    //Query 回执速率与冻结单步状态
    private static int Query(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var tickRate = source.Server.TickRate;
        source.SendSuccess($"目标刻速率 {tickRate.TickRate:F1} 每刻 {tickRate.MillisecondsPerTick:F1} 毫秒");
        if (tickRate.IsFrozen) source.SendSuccess("世界已冻结");
        if (tickRate.IsSteppingForward) source.SendSuccess($"正在单步 剩余 {tickRate.FrozenTicksToRun} 刻");
        if (tickRate.IsSprinting) source.SendSuccess($"正在冲刺 剩余 {tickRate.SprintTicksRemaining} 刻");
        return 1;
    }

    //SetRate 改目标刻速率
    private static int SetRate(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var rate = FloatArgumentType.GetFloat(context, "rate");
        source.Server.TickRate.SetTickRate(rate);
        source.SendSuccess($"目标刻速率已设为 {rate:F1}");
        return 1;
    }

    //SetFrozen 冻结或解冻世界推进 对应原版 setFreeze
    //冻结前先把在跑的冲刺与单步停掉 否则残留计数会让解冻后又白跑几拍
    private static int SetFrozen(CommandContext<CommandSourceStack> context, bool frozen)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var tickRate = source.Server.TickRate;
        if (frozen)
        {
            tickRate.StopSprinting();
            tickRate.StopStepping();
        }

        tickRate.SetFrozen(frozen);
        source.SendSuccess(frozen ? "世界已冻结" : "世界已解冻");
        return 1;
    }

    //Step 冻结状态下推进指定刻数
    private static int Step(CommandContext<CommandSourceStack> context, int ticks)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var tickRate = source.Server.TickRate;
        if (!tickRate.IsFrozen)
        {
            source.SendFailure("只有冻结状态下才能单步");
            return 0;
        }

        tickRate.StepGameIfPaused(ticks);
        source.SendSuccess($"已单步推进 {ticks} 刻");
        return ticks;
    }

    //StopStep 取消剩余单步
    private static int StopStep(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        if (!source.Server.TickRate.StopStepping())
        {
            source.SendFailure("当前没有正在进行的单步");
            return 0;
        }

        source.SendSuccess("已取消单步");
        return 1;
    }

    //Sprint 以最快速度跑指定刻数
    private static int Sprint(CommandContext<CommandSourceStack> context, int ticks)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var tickRate = source.Server.TickRate;
        if (!tickRate.RequestGameToSprint(ticks))
        {
            source.SendFailure("世界已冻结或正在冲刺");
            return 0;
        }

        source.SendSuccess($"已开始冲刺 {ticks} 刻");
        return ticks;
    }

    //StopSprint 结束冲刺
    private static int StopSprint(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        if (!source.Server.TickRate.StopSprinting())
        {
            source.SendFailure("当前没有正在进行的冲刺");
            return 0;
        }

        source.SendSuccess("已结束冲刺");
        return 1;
    }
}
