namespace NetCraft.Registry;

//Timeline 时间线对应原版 net.minecraft.world.timeline.Timeline
//把一个世界时钟划分为周期 periodTicks 并定义周期内的时间标记
//原版另有 EnvironmentAttribute 轨道用于客户端环境采样 此处不移植
public sealed class Timeline
{
    //TimeMarkerInfo 标记定义 ticks 为周期内位置 showInCommands 控制命令建议可见性
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

    //Clock 所属世界时钟
    public Holder<WorldClock> Clock { get; }

    //PeriodTicks 周期刻数 null 表示非周期时间线
    public int? PeriodTicks { get; }

    //TimeMarkers 周期内时间标记定义只读视图
    public IReadOnlyDictionary<ResourceKey<ClockTimeMarker>, TimeMarkerInfo> TimeMarkers { get; }

    //GetPeriodCount 已走过的完整周期数 非周期时间线返回 0
    public int GetPeriodCount(ClockManager clockManager)
    {
        if (PeriodTicks is null)
            return 0;
        return (int)(GetTotalTicks(clockManager) / PeriodTicks.Value);
    }

    //GetCurrentTicks 当前周期内刻数 非周期时间线返回总刻数
    public long GetCurrentTicks(ClockManager clockManager)
    {
        var totalTicks = GetTotalTicks(clockManager);
        return PeriodTicks is null ? totalTicks : totalTicks % PeriodTicks.Value;
    }

    public long GetTotalTicks(ClockManager clockManager) => clockManager.GetTotalTicks(Clock);

    //RegisterTimeMarkers 把周期内标记展开成 ClockTimeMarker 交给回调注册
    public void RegisterTimeMarkers(Action<ResourceKey<ClockTimeMarker>, ClockTimeMarker> output)
    {
        foreach (var (key, info) in _timeMarkers)
            output(key, new ClockTimeMarker(Clock, info.Ticks, PeriodTicks, info.ShowInCommands));
    }
}
