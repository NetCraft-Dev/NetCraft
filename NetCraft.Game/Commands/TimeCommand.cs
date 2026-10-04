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

//TimeCommand time 命令对应原版 net.minecraft.server.commands.TimeCommand
//默认作用于维度默认时钟 time of <clock> 显式指定时钟
//set 支持数值与时间标记 add 支持负数 pause/resume/rate 控制流速
public static class TimeCommand
{
    private const float MinClockRate = 1e-5f;
    private const float MaxClockRate = 1000f;

    public static readonly DynamicCommandExceptionType ErrorNoDefaultClock =
        new(dimension => new LiteralMessage($"维度 {dimension} 未配置默认时钟"));

    public static readonly Dynamic2CommandExceptionType ErrorNoTimeMarkerFound =
        new((clock, timeMarker) => new LiteralMessage($"时钟 {clock} 没有时间标记 {timeMarker}"));

    public static readonly Dynamic2CommandExceptionType ErrorWrongTimelineForClock =
        new((clock, timeline) => new LiteralMessage($"时间线 {timeline} 不属于时钟 {clock}"));

    //ClockGetter 从上下文取目标时钟 默认分支走维度默认时钟 of 分支走 resource 参数
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

    //OfClockArgument time of 分支 clock 参数下挂完整时钟子树 目标取 resource 参数
    private static RequiredArgumentBuilder<CommandSourceStack, Identifier> OfClockArgument()
    {
        var clock = RequiredArgumentBuilder<CommandSourceStack, Identifier>.Argument(
            "clock", new ResourceArgument(Registries.WORLD_CLOCK.Identifier));
        AddClockNodes(clock, c => ResourceArgument.GetClock(c, "clock"));
        return clock;
    }

    //AddClockNodes 挂 set/add/pause/resume/rate/query 子树 自限定泛型对齐原版 A extends ArgumentBuilder
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

    //SuggestTimeMarkers 建议该时钟下命令可见的时间标记
    //原版走 SharedSuggestionProvider.suggestResource 按 remaining 前缀过滤后给完整标识符
    private static Task<Suggestions> SuggestTimeMarkers(CommandContext<CommandSourceStack> context, SuggestionsBuilder builder, Holder<WorldClock> clock)
    {
        var source = RequireSource(context);
        if (source is not null)
            foreach (var marker in source.Server.ClockManager.CommandTimeMarkersForClock(clock))
                AddIfMatches(builder, marker.Identifier.ToString());
        return builder.BuildFuture();
    }

    //SuggestTimelines 建议挂在该时钟上的时间线
    private static Task<Suggestions> SuggestTimelines(CommandContext<CommandSourceStack> context, SuggestionsBuilder builder, Holder<WorldClock> clock)
    {
        foreach (var holder in BuiltInRegistries.TIMELINE.ListElements())
            if (ReferenceEquals(holder.Value.Clock, clock))
                AddIfMatches(builder, holder.RegisteredName);
        return builder.BuildFuture();
    }

    //AddIfMatches 按当前输入前缀过滤后加入候选 对应原版 filterResources 的大小写不敏感前缀匹配
    private static void AddIfMatches(SuggestionsBuilder builder, string text)
    {
        if (text.StartsWith(builder.RemainingLowerCase, StringComparison.Ordinal))
            builder.Add(text);
    }

    private static ServerCommandSource? RequireSource(CommandContext<CommandSourceStack> context)
        => context.GetSource() as ServerCommandSource;

    //QueryGameTime time query gametime 回执关卡游戏刻
    private static int QueryGameTime(CommandContext<CommandSourceStack> context)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        var gameTime = source.PlayerOrThrow.Level.GameTime;
        source.SendSuccess($"游戏时间 {gameTime}");
        return WrapTime(gameTime);
    }

    //QueryTime time query time 回执时钟总刻数
    private static int QueryTime(ServerCommandSource source, Holder<WorldClock> clock)
    {
        var totalTicks = source.Server.ClockManager.GetTotalTicks(clock);
        source.SendSuccess($"时钟 {clock.RegisteredName} 的时间为 {totalTicks}");
        return WrapTime(totalTicks);
    }

    //QueryTimelineTicks time query <timeline> 回执时间线当前刻
    private static int QueryTimelineTicks(ServerCommandSource source, Holder<WorldClock> clock, Holder<Timeline> timeline)
    {
        if (!ReferenceEquals(timeline.Value.Clock, clock))
            throw ErrorWrongTimelineForClock.Create(clock.RegisteredName, timeline.RegisteredName);
        var currentTicks = timeline.Value.GetCurrentTicks(source.Server.ClockManager);
        source.SendSuccess($"时间线 {timeline.RegisteredName} 的当前刻为 {currentTicks}");
        return WrapTime(currentTicks);
    }

    //QueryTimelineRepetitions time query <timeline> repetition 回执时间线完整周期数
    private static int QueryTimelineRepetitions(ServerCommandSource source, Holder<WorldClock> clock, Holder<Timeline> timeline)
    {
        if (!ReferenceEquals(timeline.Value.Clock, clock))
            throw ErrorWrongTimelineForClock.Create(clock.RegisteredName, timeline.RegisteredName);
        var repetitions = timeline.Value.GetPeriodCount(source.Server.ClockManager);
        source.SendSuccess($"时间线 {timeline.RegisteredName} 已完成 {repetitions} 个周期");
        return WrapTime(repetitions);
    }

    //SetTotalTicks time set <time> 直接设总刻数
    private static int SetTotalTicks(ServerCommandSource source, Holder<WorldClock> clock, int totalTicks)
    {
        source.Server.ClockManager.SetTotalTicks(clock, totalTicks);
        source.SendSuccess($"已将时钟 {clock.RegisteredName} 设为 {totalTicks}");
        return totalTicks;
    }

    //AddTime time add <time> 累加刻数下限为零
    private static int AddTime(ServerCommandSource source, Holder<WorldClock> clock, int time)
    {
        source.Server.ClockManager.AddTicks(clock, time);
        var totalTicks = source.Server.ClockManager.GetTotalTicks(clock);
        source.SendSuccess($"时钟 {clock.RegisteredName} 的时间为 {totalTicks}");
        return WrapTime(totalTicks);
    }

    //SetTimeToTimeMarker time set <timemarker> 跳到下一次该时间标记
    private static int SetTimeToTimeMarker(ServerCommandSource source, Holder<WorldClock> clock, ResourceKey<ClockTimeMarker> timeMarkerId)
    {
        if (!source.Server.ClockManager.MoveToTimeMarker(clock, timeMarkerId))
            throw ErrorNoTimeMarkerFound.Create(clock.RegisteredName, timeMarkerId.Identifier);
        source.SendSuccess($"已将时钟 {clock.RegisteredName} 设为 {timeMarkerId.Identifier}");
        return WrapTime(source.Server.ClockManager.GetTotalTicks(clock));
    }

    //SetPaused time pause/resume 暂停或恢复时钟
    private static int SetPaused(ServerCommandSource source, Holder<WorldClock> clock, bool paused)
    {
        source.Server.ClockManager.SetPaused(clock, paused);
        source.SendSuccess($"时钟 {clock.RegisteredName} {(paused ? "已暂停" : "已恢复")}");
        return 1;
    }

    //SetRate time rate <rate> 设时钟倍率
    private static int SetRate(ServerCommandSource source, Holder<WorldClock> clock, float rate)
    {
        source.Server.ClockManager.SetRate(clock, rate);
        source.SendSuccess($"时钟 {clock.RegisteredName} 的倍率已设为 {rate}");
        return 1;
    }

    //GetDefaultClock 取维度默认时钟 未配置抛异常
    private static Holder<WorldClock> GetDefaultClock(CommandContext<CommandSourceStack> context)
    {
        var source = RequireSource(context);
        if (source is null) throw ErrorNoDefaultClock.Create("unknown");
        var level = source.PlayerOrThrow.Level;
        return level.DefaultClock ?? throw ErrorNoDefaultClock.Create(level.Dimension);
    }

    //WrapTime 刻数折算进 int 返回值 对应原版 wrapTime
    private static int WrapTime(long ticks)
        => (int)(ticks % int.MaxValue);
}
