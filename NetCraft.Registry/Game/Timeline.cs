namespace NetCraft.Registry;

//Timeline timeline, maps to vanilla net.minecraft.world.timeline.Timeline
//Divides a world clock into periods (periodTicks) and defines the time markers within a period
//Vanilla also has EnvironmentAttribute tracks for client environment sampling, not ported here
public sealed class Timeline
{
    //TimeMarkerInfo marker definition; ticks is the position within the period and showInCommands controls command suggestion visibility
    public sealed record TimeMarkerInfo(int Ticks, bool ShowInCommands);

    private readonly Dictionary<ResourceKey<ClockTimeMarker>, TimeMarkerInfo> _timeMarkers;

    public Timeline(Holder<WorldClock> clock, int? periodTicks,
        IEnumerable<KeyValuePair<ResourceKey<ClockTimeMarker>, TimeMarkerInfo>>? timeMarkers = null)
    {
        Clock = clock;
        PeriodTicks = periodTicks;
        _timeMarkers = timeMarkers is null
            ? new Dictionary<ResourceKey<ClockTimeMarker>, TimeMarkerInfo>()
            : new Dictionary<ResourceKey<ClockTimeMarker>, TimeMarkerInfo>(timeMarkers);
        TimeMarkers = _timeMarkers;
    }

    //Clock the owning world clock
    public Holder<WorldClock> Clock { get; }

    //PeriodTicks period tick count; null means an aperiodic timeline
    public int? PeriodTicks { get; }

    //TimeMarkers read-only view of the time marker definitions within the period
    public IReadOnlyDictionary<ResourceKey<ClockTimeMarker>, TimeMarkerInfo> TimeMarkers { get; }

    //GetPeriodCount number of complete periods elapsed; returns 0 for an aperiodic timeline
    public int GetPeriodCount(ClockManager clockManager)
    {
        if (PeriodTicks is null)
            return 0;
        return (int)(GetTotalTicks(clockManager) / PeriodTicks.Value);
    }

    //GetCurrentTicks ticks within the current period; returns total ticks for an aperiodic timeline
    public long GetCurrentTicks(ClockManager clockManager)
    {
        var totalTicks = GetTotalTicks(clockManager);
        return PeriodTicks is null ? totalTicks : totalTicks % PeriodTicks.Value;
    }

    public long GetTotalTicks(ClockManager clockManager) => clockManager.GetTotalTicks(Clock);

    //RegisterTimeMarkers expands the markers within the period into ClockTimeMarkers and hands them to the callback to register
    public void RegisterTimeMarkers(Action<ResourceKey<ClockTimeMarker>, ClockTimeMarker> output)
    {
        foreach (var (key, info) in _timeMarkers)
            output(key, new ClockTimeMarker(Clock, info.Ticks, PeriodTicks, info.ShowInCommands));
    }
}
