using NetCraft.Config;
using NetCraft.Logging;

namespace NetCraft.Util.Profiling;

//Single-tick profiler, maps to vanilla net.minecraft.util.profiling.SingleTickProfiler
//Ticks exceeding the time threshold are written to a file
public sealed class SingleTickProfiler
{
    private readonly Func<long> _realTime;
    private readonly long _saveThreshold;
    private int _tick;
    private readonly string _location;
    private ProfileCollector _profiler = InactiveProfiler.Instance;

    public SingleTickProfiler(Func<long> realTime, string location, long saveThresholdNs)
    {
        _realTime = realTime;
        _location = location;
        _saveThreshold = saveThresholdNs;
    }

    public ProfilerFiller StartTick()
    {
        _profiler = new ActiveProfiler(_realTime, () => _tick, () => true);
        _tick++;
        return _profiler;
    }

    public void EndTick()
    {
        if (ReferenceEquals(_profiler, InactiveProfiler.Instance)) return;
        var results = _profiler.GetResults();
        _profiler = InactiveProfiler.Instance;
        if (results.NanoDuration >= _saveThreshold)
        {
            var dir = Path.Combine("debug", _location);
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"tick-results-{ProfilingUtil.GetFilenameFormattedDateTime()}.txt");
            results.SaveResults(file);
            Log.Info($"Recorded long tick -- wrote info to: {Path.GetFullPath(file)}");
        }
    }

    //Creates a tick profiler, maps to vanilla createTickProfiler gated by DEBUG_MONITOR_TICK_TIMES
    public static SingleTickProfiler? CreateTickProfiler(string name)
    {
        //NetCraft has no DEBUG_MONITOR_TICK_TIMES constant yet, so nothing is created by default
        return null;
    }

    //Merges fillers, maps to vanilla decorateFiller
    public static ProfilerFiller DecorateFiller(ProfilerFiller filler, SingleTickProfiler? tickProfiler)
    {
        if (tickProfiler is not null)
        {
            return ProfilerFiller.Combine(tickProfiler.StartTick(), filler);
        }
        return filler;
    }
}
