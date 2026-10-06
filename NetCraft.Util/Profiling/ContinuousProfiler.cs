namespace NetCraft.Util.Profiling;

//Continuous profiler, maps to vanilla net.minecraft.util.profiling.ContinuousProfiler
//Wraps ActiveProfiler, enable/disable controls the sampling window
public sealed class ContinuousProfiler
{
    private readonly Func<long> _realTime;
    private readonly Func<int> _tickCount;
    private readonly Func<bool> _suppressWarnings;
    private ProfileCollector _profiler;

    public ContinuousProfiler(Func<long> realTime, Func<int> tickCount, Func<bool> suppressWarnings)
    {
        _realTime = realTime;
        _tickCount = tickCount;
        _suppressWarnings = suppressWarnings;
        _profiler = InactiveProfiler.Instance;
    }

    public bool IsEnabled() => !ReferenceEquals(_profiler, InactiveProfiler.Instance);

    public void Disable() => _profiler = InactiveProfiler.Instance;

    public void Enable()
        => _profiler = new ActiveProfiler(_realTime, _tickCount, _suppressWarnings);

    public ProfilerFiller GetFiller() => _profiler;

    public ProfileResults GetResults() => _profiler.GetResults();
}
