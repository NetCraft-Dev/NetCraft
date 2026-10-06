namespace NetCraft.Registry;

//ClockTimeMarker clock time marker, maps to vanilla net.minecraft.world.clock.ClockTimeMarker
//Defined by Timeline and registered to ServerClockManager for time set <marker> jumps and suggestion lists
//With periodTicks it is located by modulo; without a period it is an absolute tick
public sealed record ClockTimeMarker(
    Holder<WorldClock> Clock,
    int Ticks,
    int? PeriodTicks,
    bool ShowInCommands)
{
    //GetRepetitionCount the number of times this marker occurs within totalTicks
    public long GetRepetitionCount(long totalTicks)
    {
        if (PeriodTicks is null)
            return totalTicks >= Ticks ? 1 : 0;
        var period = PeriodTicks.Value;
        return totalTicks / period + (totalTicks % period >= Ticks ? 1 : 0);
    }

    //ResolveTimeToMoveTo computes the total ticks needed to jump to the marker's next occurrence
    public long ResolveTimeToMoveTo(long totalTicks)
    {
        if (PeriodTicks is null)
            return Ticks;
        var period = PeriodTicks.Value;
        return totalTicks + DurationToNext(period, totalTicks % period, Ticks);
    }

    //OccursAt whether totalTicks lands exactly on this marker
    public bool OccursAt(long totalTicks)
        => PeriodTicks is null ? Ticks == totalTicks : Ticks == totalTicks % PeriodTicks.Value;

    //DurationToNext forward distance from from to to; wraps a full period if to has not passed
    private static long DurationToNext(int periodTicks, long from, int to)
    {
        var duration = (long)to - from;
        return duration > 0 ? duration : periodTicks + duration;
    }
}
