using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Suggestion;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.World.Clock;
using NetCraft.Registry;

namespace NetCraft.Game.Commands;

//TimeCommand time command, maps to vanilla net.minecraft.server.commands.TimeCommand
//By default it acts on the dimension's default clock; time of <clock> selects the clock explicitly
//set supports values and time markers; add supports negatives; pause/resume/rate control the flow rate
public static class TimeCommand
{
    private const float MinClockRate = 1e-5f;
    private const float MaxClockRate = 1000f;

    public static readonly DynamicCommandExceptionType ErrorNoDefaultClock =
        new(dimension => new LiteralMessage($"dimension {dimension} has no default clock configured"));

    public static readonly Dynamic2CommandExceptionType ErrorNoTimeMarkerFound =
        new((clock, timeMarker) => new LiteralMessage($"clock {clock} has no time marker {timeMarker}"));

    public static readonly Dynamic2CommandExceptionType ErrorWrongTimelineForClock =
        new((clock, timeline) => new LiteralMessage($"timeline {timeline} does not belong to clock {clock}"));

    //ClockGetter takes the target clock from the context; the default branch uses the dimension default clock, the of branch uses the resource argument
    public delegate Holder<WorldClock> ClockGetter(CommandContext<CommandSourceStack> context);

    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        var baseCommand = LiteralArgumentBuilder<CommandSourceStack>.Literal("time")
            .Requires(s => s.HasPermission(2));
        AddClockNodes(baseCommand, GetDefaultClock);
        baseCommand
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("query")
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("gametime")
                    .Executes(QueryGameTime)))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("of")
                .Then(OfClockArgument()));
        dispatcher.Register(baseCommand);
    }

    //OfClockArgument the clock argument of the time of branch has the full clock subtree; the target comes from the resource argument
    private static RequiredArgumentBuilder<CommandSourceStack, Identifier> OfClockArgument()
    {
        var clock = RequiredArgumentBuilder<CommandSourceStack, Identifier>.Argument(
            "clock", new ResourceArgument(Registries.WORLD_CLOCK.Identifier));
        AddClockNodes(clock, c => ResourceArgument.GetClock(c, "clock"));
        return clock;
    }

    //AddClockNodes hangs the set/add/pause/resume/rate/query subtrees; the self-referential generic matches vanilla A extends ArgumentBuilder
    private static T AddClockNodes<T>(T node, ClockGetter clockGetter) where T : ArgumentBuilder<CommandSourceStack, T>
    {
        node.Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("set")
            .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("time", TimeArgument.Time())
                .Executes(c => SetTotalTicks((ServerCommandSource)c.GetSource(), clockGetter(c), IntegerArgumentType.GetInteger(c, "time"))))
            .Then(RequiredArgumentBuilder<CommandSourceStack, Identifier>.Argument("timemarker", IdentifierArgument.Id())
                .Suggests((c, b) => SuggestTimeMarkers(c, b, clockGetter(c)))
                .Executes(c => SetTimeToTimeMarker((ServerCommandSource)c.GetSource(), clockGetter(c),
                    ResourceKey<ClockTimeMarker>.Create(Registries.CLOCK_TIME_MARKER, IdentifierArgument.GetId(c, "timemarker"))))));
        node.Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("add")
            .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("time", TimeArgument.Time(int.MinValue))
                .Executes(c => AddTime((ServerCommandSource)c.GetSource(), clockGetter(c), IntegerArgumentType.GetInteger(c, "time")))));
        node.Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("pause")
            .Executes(c => SetPaused((ServerCommandSource)c.GetSource(), clockGetter(c), true)));
        node.Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("resume")
            .Executes(c => SetPaused((ServerCommandSource)c.GetSource(), clockGetter(c), false)));
        node.Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("rate")
            .Then(RequiredArgumentBuilder<CommandSourceStack, float>.Argument("rate", FloatArgumentType.FloatArg(MinClockRate, MaxClockRate))
                .Executes(c => SetRate((ServerCommandSource)c.GetSource(), clockGetter(c), FloatArgumentType.GetFloat(c, "rate")))));
        node.Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("query")
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("time")
                .Executes(c => QueryTime((ServerCommandSource)c.GetSource(), clockGetter(c))))
            .Then(RequiredArgumentBuilder<CommandSourceStack, Identifier>.Argument("timeline", new ResourceArgument(Registries.TIMELINE.Identifier))
                .Suggests((c, b) => SuggestTimelines(c, b, clockGetter(c)))
                .Executes(c => QueryTimelineTicks((ServerCommandSource)c.GetSource(), clockGetter(c), ResourceArgument.GetTimeline(c, "timeline")))
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("repetition")
                    .Executes(c => QueryTimelineRepetitions((ServerCommandSource)c.GetSource(), clockGetter(c), ResourceArgument.GetTimeline(c, "timeline"))))));
        return node;
    }

    //SuggestTimeMarkers suggests the command-visible time markers of the clock
    //Vanilla uses SharedSuggestionProvider.suggestResource, filtering by the remaining prefix then giving the full identifier
    private static Task<Suggestions> SuggestTimeMarkers(CommandContext<CommandSourceStack> context, SuggestionsBuilder builder, Holder<WorldClock> clock)
    {
        var source = RequireSource(context);
        if (source is not null)
            foreach (var marker in source.Server.ClockManager.CommandTimeMarkersForClock(clock))
                AddIfMatches(builder, marker.Identifier.ToString());
        return builder.BuildFuture();
    }

    //SuggestTimelines suggests the timelines attached to the clock
    private static Task<Suggestions> SuggestTimelines(CommandContext<CommandSourceStack> context, SuggestionsBuilder builder, Holder<WorldClock> clock)
    {
        foreach (var holder in BuiltInRegistries.TIMELINE.ListElements())
            if (ReferenceEquals(holder.Value.Clock, clock))
                AddIfMatches(builder, holder.RegisteredName);
        return builder.BuildFuture();
    }

    //AddIfMatches filters by the current input prefix then adds to the candidates, maps to vanilla filterResources case-insensitive prefix match
    private static void AddIfMatches(SuggestionsBuilder builder, string text)
    {
        if (text.StartsWith(builder.RemainingLowerCase, StringComparison.Ordinal))
            builder.Add(text);
    }

    private static ServerCommandSource? RequireSource(CommandContext<CommandSourceStack> context)
        => context.GetSource() as ServerCommandSource;

    //QueryGameTime time query gametime reports the level game time
    private static int QueryGameTime(CommandContext<CommandSourceStack> context)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        var gameTime = source.PlayerOrThrow.Level.GameTime;
        source.SendSuccess($"game time {gameTime}");
        return WrapTime(gameTime);
    }

    //QueryTime time query time reports the clock's total ticks
    private static int QueryTime(ServerCommandSource source, Holder<WorldClock> clock)
    {
        var totalTicks = source.Server.ClockManager.GetTotalTicks(clock);
        source.SendSuccess($"clock {clock.RegisteredName} time is {totalTicks}");
        return WrapTime(totalTicks);
    }

    //QueryTimelineTicks time query <timeline> reports the timeline's current ticks
    private static int QueryTimelineTicks(ServerCommandSource source, Holder<WorldClock> clock, Holder<Timeline> timeline)
    {
        if (!ReferenceEquals(timeline.Value.Clock, clock))
            throw ErrorWrongTimelineForClock.Create(clock.RegisteredName, timeline.RegisteredName);
        var currentTicks = timeline.Value.GetCurrentTicks(source.Server.ClockManager);
        source.SendSuccess($"timeline {timeline.RegisteredName} current tick is {currentTicks}");
        return WrapTime(currentTicks);
    }

    //QueryTimelineRepetitions time query <timeline> repetition reports the timeline's full period count
    private static int QueryTimelineRepetitions(ServerCommandSource source, Holder<WorldClock> clock, Holder<Timeline> timeline)
    {
        if (!ReferenceEquals(timeline.Value.Clock, clock))
            throw ErrorWrongTimelineForClock.Create(clock.RegisteredName, timeline.RegisteredName);
        var repetitions = timeline.Value.GetPeriodCount(source.Server.ClockManager);
        source.SendSuccess($"timeline {timeline.RegisteredName} has completed {repetitions} periods");
        return WrapTime(repetitions);
    }

    //SetTotalTicks time set <time> sets the total ticks directly
    private static int SetTotalTicks(ServerCommandSource source, Holder<WorldClock> clock, int totalTicks)
    {
        source.Server.ClockManager.SetTotalTicks(clock, totalTicks);
        source.SendSuccess($"set clock {clock.RegisteredName} to {totalTicks}");
        return totalTicks;
    }

    //AddTime time add <time> accumulates ticks with a lower bound of zero
    private static int AddTime(ServerCommandSource source, Holder<WorldClock> clock, int time)
    {
        source.Server.ClockManager.AddTicks(clock, time);
        var totalTicks = source.Server.ClockManager.GetTotalTicks(clock);
        source.SendSuccess($"clock {clock.RegisteredName} time is {totalTicks}");
        return WrapTime(totalTicks);
    }

    //SetTimeToTimeMarker time set <timemarker> jumps to the next such time marker
    private static int SetTimeToTimeMarker(ServerCommandSource source, Holder<WorldClock> clock, ResourceKey<ClockTimeMarker> timeMarkerId)
    {
        if (!source.Server.ClockManager.MoveToTimeMarker(clock, timeMarkerId))
            throw ErrorNoTimeMarkerFound.Create(clock.RegisteredName, timeMarkerId.Identifier);
        source.SendSuccess($"set clock {clock.RegisteredName} to {timeMarkerId.Identifier}");
        return WrapTime(source.Server.ClockManager.GetTotalTicks(clock));
    }

    //SetPaused time pause/resume pauses or resumes the clock
    private static int SetPaused(ServerCommandSource source, Holder<WorldClock> clock, bool paused)
    {
        source.Server.ClockManager.SetPaused(clock, paused);
        source.SendSuccess($"clock {clock.RegisteredName} {(paused ? "paused" : "resumed")}");
        return 1;
    }

    //SetRate time rate <rate> sets the clock rate
    private static int SetRate(ServerCommandSource source, Holder<WorldClock> clock, float rate)
    {
        source.Server.ClockManager.SetRate(clock, rate);
        source.SendSuccess($"clock {clock.RegisteredName} rate set to {rate}");
        return 1;
    }

    //GetDefaultClock gets the dimension default clock; throws when not configured
    private static Holder<WorldClock> GetDefaultClock(CommandContext<CommandSourceStack> context)
    {
        var source = RequireSource(context);
        if (source is null) throw ErrorNoDefaultClock.Create("unknown");
        var level = source.PlayerOrThrow.Level;
        return level.DefaultClock ?? throw ErrorNoDefaultClock.Create(level.Dimension);
    }

    //WrapTime wraps the tick count into the int return value, maps to vanilla wrapTime
    private static int WrapTime(long ticks)
        => (int)(ticks % int.MaxValue);
}
