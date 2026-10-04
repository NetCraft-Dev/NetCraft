namespace NetCraft.Registry;

//ClockTimeMarker 时钟时间标记对应原版 net.minecraft.world.clock.ClockTimeMarker
//由 Timeline 定义注册到 ServerClockManager 供 time set <marker> 跳转与建议列表
//有 periodTicks 时按周期取模定位 无周期时为绝对刻
public sealed record ClockTimeMarker(
    Holder<WorldClock> Clock,
    int Ticks,
    int? PeriodTicks,
    bool ShowInCommands)
{
    //GetRepetitionCount totalTicks 内该标记出现的次数
    public long GetRepetitionCount(long totalTicks)
    {
        if (PeriodTicks is null)
            return totalTicks >= Ticks ? 1 : 0;
        var period = PeriodTicks.Value;
        return totalTicks / period + (totalTicks % period >= Ticks ? 1 : 0);
    }

    //ResolveTimeToMoveTo 计算跳到下一次该标记需要的总刻数
    public long ResolveTimeToMoveTo(long totalTicks)
    {
        if (PeriodTicks is null)
            return Ticks;
        var period = PeriodTicks.Value;
        return totalTicks + DurationToNext(period, totalTicks % period, Ticks);
    }

    //OccursAt 判断 totalTicks 是否正好落在该标记上
    public bool OccursAt(long totalTicks)
        => PeriodTicks is null ? Ticks == totalTicks : Ticks == totalTicks % PeriodTicks.Value;

    //DurationToNext 从 from 到 to 的正向距离 to 未过则绕一整圈
    private static long DurationToNext(int periodTicks, long from, int to)
    {
        var duration = (long)to - from;
        return duration > 0 ? duration : periodTicks + duration;
    }
}
