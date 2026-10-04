using NetCraft.Codec;
using NetCraft.Logging;
using NetCraft.Util.Profiling.Metrics;

namespace NetCraft.Util.Profiling;

//活跃profiler对应原版net.minecraft.util.profiling.ActiveProfiler
//记录路径耗时/counters/chartedPaths并生成FilledProfileResults
public sealed class ActiveProfiler : ProfileCollector
{
    private const long WarningTimeNanos = 100_000_000L; //100ms纳秒

    private readonly Func<int> _getTickTime;
    private readonly Func<long> _getRealTime;
    private readonly long _startTimeNano;
    private readonly int _startTimeTicks;
    private bool _started;
    private PathEntry? _currentEntry;
    private readonly Func<bool> _suppressWarnings;
    private readonly List<string> _paths = new();
    private readonly List<long> _startTimes = new();
    private readonly Dictionary<string, ProfilerPathEntry> _entries = new();
    private string _path = string.Empty;
    private readonly HashSet<Pair<string, MetricCategory>> _chartedPaths = new();

    public ActiveProfiler(Func<long> getRealTime, Func<int> getTickTime, Func<bool> suppressWarnings)
    {
        _startTimeNano = getRealTime();
        _getRealTime = getRealTime;
        _startTimeTicks = getTickTime();
        _getTickTime = getTickTime;
        _suppressWarnings = suppressWarnings;
    }

    public void StartTick()
    {
        if (_started)
        {
            Log.Error("Profiler tick already started - missing endTick()?");
            return;
        }
        _started = true;
        _path = string.Empty;
        _paths.Clear();
        Push(ProfilerFiller.Root);
    }

    public void EndTick()
    {
        if (!_started)
        {
            Log.Error("Profiler tick already ended - missing startTick()?");
            return;
        }
        Pop();
        _started = false;
        if (!string.IsNullOrEmpty(_path))
        {
            Log.Error($"Profiler tick ended before path was fully popped (remainder: '{ProfileResults.DemanglePath(_path)}'). Mismatched push/pop?");
        }
    }

    public void Push(string name)
    {
        if (!_started)
        {
            Log.Error($"Cannot push '{name}' to profiler if profiler tick hasn't started - missing startTick()?");
            return;
        }
        if (!string.IsNullOrEmpty(_path)) _path += '\x1e';
        _path += name;
        _paths.Add(_path);
        _startTimes.Add(ProfilingUtil.GetNanos());
        _currentEntry = null;
    }

    public void Push(Func<string> name) => Push(name());

    public void MarkForCharting(MetricCategory category)
    {
        _chartedPaths.Add(Pair<string, MetricCategory>.Of(_path, category));
    }

    public void Pop()
    {
        if (!_started)
        {
            Log.Error("Cannot pop from profiler if profiler tick hasn't started - missing startTick()?");
            return;
        }
        if (_startTimes.Count == 0)
        {
            Log.Error("Tried to pop one too many times! Mismatched push() and pop()?");
            return;
        }
        var endTime = ProfilingUtil.GetNanos();
        var startTime = _startTimes[^1];
        _startTimes.RemoveAt(_startTimes.Count - 1);
        _paths.RemoveAt(_paths.Count - 1);
        var time = endTime - startTime;
        var currentEntry = GetCurrentEntry();
        currentEntry._accumulatedDuration += time;
        currentEntry._count++;
        currentEntry._maxDuration = Math.Max(currentEntry._maxDuration, time);
        currentEntry._minDuration = Math.Min(currentEntry._minDuration, time);
        if (time > WarningTimeNanos && !_suppressWarnings())
        {
            Log.Warning($"Something's taking too long! '{ProfileResults.DemanglePath(_path)}' took aprox {(time / 1_000_000.0d).ToString("F3")} ms");
        }
        _path = _paths.Count == 0 ? string.Empty : _paths[^1];
        _currentEntry = null;
    }

    public void PopPush(string name)
    {
        Pop();
        Push(name);
    }

    public void PopPush(Func<string> name)
    {
        Pop();
        Push(name());
    }

    private PathEntry GetCurrentEntry()
    {
        if (_currentEntry is null)
        {
            if (!_entries.TryGetValue(_path, out var baseEntry) || baseEntry is not PathEntry entry)
            {
                entry = new PathEntry();
                _entries[_path] = entry;
            }
            _currentEntry = entry;
        }
        return _currentEntry;
    }

    public void IncrementCounter(string name, int amount)
    {
        var counters = GetCurrentEntry()._counters;
        counters[name] = counters.GetValueOrDefault(name) + amount;
    }

    public void IncrementCounter(Func<string> name, int amount) => IncrementCounter(name(), amount);

    public ProfileResults GetResults()
        => new FilledProfileResults(_entries, _startTimeNano, _startTimeTicks, _getRealTime(), _getTickTime());

    public ProfilerPathEntry? GetEntry(string path)
        => _entries.TryGetValue(path, out var entry) ? entry : null;

    public ISet<Pair<string, MetricCategory>> GetChartedPaths() => _chartedPaths;

    //路径条目对应原版ActiveProfiler.PathEntry
    //累积耗时/调用次数/最大最小/counters
    public sealed class PathEntry : ProfilerPathEntry
    {
        internal long _accumulatedDuration;
        internal long _count;
        internal long _maxDuration = long.MinValue;
        internal long _minDuration = long.MaxValue;
        internal readonly Dictionary<string, long> _counters = new();

        public long Duration => _accumulatedDuration;
        public long MaxDuration => _maxDuration;
        public long Count => _count;
        public IReadOnlyDictionary<string, long> Counters => _counters;
    }
}
