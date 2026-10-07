using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;

namespace NetCraft.Game.Commands;

//TickCommand tick command, maps to vanilla net.minecraft.server.commands.TickCommand
//query shows the current tick status; rate changes the rate; freeze/unfreeze freezes the world; step/sprint single-step and sprint
public static class TickCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        var query = LiteralArgumentBuilder<CommandSourceStack>.Literal("query")
            .Executes(Query);

        var rate = LiteralArgumentBuilder<CommandSourceStack>.Literal("rate")
            .Then(RequiredArgumentBuilder<CommandSourceStack, float>.Argument("rate", FloatArgumentType.FloatArg(1f, 10000f))
                .Executes(SetRate));

        //The stop of step and sprint hangs under themselves, maps to vanilla /tick step stop and /tick sprint stop
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

    //Query reports the rate and frozen/step status
    private static int Query(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var tickRate = source.Server.TickRate;
        source.SendSuccess($"target tick rate {tickRate.TickRate:F1}, {tickRate.MillisecondsPerTick:F1} ms per tick");
        if (tickRate.IsFrozen) source.SendSuccess("the world is frozen");
        if (tickRate.IsSteppingForward) source.SendSuccess($"stepping, {tickRate.FrozenTicksToRun} ticks remaining");
        if (tickRate.IsSprinting) source.SendSuccess($"sprinting, {tickRate.SprintTicksRemaining} ticks remaining");
        return 1;
    }

    //SetRate changes the target tick rate
    private static int SetRate(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var rate = FloatArgumentType.GetFloat(context, "rate");
        source.Server.TickRate.SetTickRate(rate);
        source.SendSuccess($"target tick rate set to {rate:F1}");
        return 1;
    }

    //SetFrozen freezes or unfreezes world advancement, maps to vanilla setFreeze
    //Before freezing it stops the running sprint and step, or leftover counts would idle a few ticks after unfreezing
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
        source.SendSuccess(frozen ? "the world is frozen" : "the world is unfrozen");
        return 1;
    }

    //Step advances the given number of ticks while frozen
    private static int Step(CommandContext<CommandSourceStack> context, int ticks)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var tickRate = source.Server.TickRate;
        if (!tickRate.IsFrozen)
        {
            source.SendFailure("single-step is only possible while frozen");
            return 0;
        }

        tickRate.StepGameIfPaused(ticks);
        source.SendSuccess($"single-stepped {ticks} ticks");
        return ticks;
    }

    //StopStep cancels the remaining step
    private static int StopStep(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        if (!source.Server.TickRate.StopStepping())
        {
            source.SendFailure("there is no single-step in progress");
            return 0;
        }

        source.SendSuccess("single-step cancelled");
        return 1;
    }

    //Sprint runs the given number of ticks as fast as possible
    private static int Sprint(CommandContext<CommandSourceStack> context, int ticks)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var tickRate = source.Server.TickRate;
        if (!tickRate.RequestGameToSprint(ticks))
        {
            source.SendFailure("the world is frozen or a sprint is already running");
            return 0;
        }

        source.SendSuccess($"started sprinting {ticks} ticks");
        return ticks;
    }

    //StopSprint ends the sprint
    private static int StopSprint(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        if (!source.Server.TickRate.StopSprinting())
        {
            source.SendFailure("there is no sprint in progress");
            return 0;
        }

        source.SendSuccess("sprint ended");
        return 1;
    }
}
