using System.Globalization;
using System.Text;
using NetCraft.Config;
using NetCraft.Logging;

namespace NetCraft.Util.Profiling;

//填充profiler结果对应原版net.minecraft.util.profiling.FilledProfileResults
//基于entries map生成ResultField列表与文本输出/CSV保存
public sealed class FilledProfileResults : ProfileResults
{
    private static readonly ProfilerPathEntry EmptyEntry = new EmptyProfilerPathEntry();

    private readonly IReadOnlyDictionary<string, ProfilerPathEntry> _entries;
    private readonly long _startTimeNano;
    private readonly int _startTimeTicks;
    private readonly long _endTimeNano;
    private readonly int _endTimeTicks;
    private readonly int _tickDuration;

    public FilledProfileResults(IReadOnlyDictionary<string, ProfilerPathEntry> entries, long startTimeNano, int startTimeTicks, long endTimeNano, int endTimeTicks)
    {
        _entries = entries;
        _startTimeNano = startTimeNano;
        _startTimeTicks = startTimeTicks;
        _endTimeNano = endTimeNano;
        _endTimeTicks = endTimeTicks;
        _tickDuration = endTimeTicks - startTimeTicks;
    }

    private ProfilerPathEntry GetEntry(string path)
        => _entries.TryGetValue(path, out var entry) ? entry : EmptyEntry;

    public List<ResultField> GetTimes(string path)
    {
        var rootEntry = GetEntry(ProfilerFiller.Root);
        var globalTime = rootEntry.Duration;
        var currentEntry = GetEntry(path);
        var selfTime = currentEntry.Duration;
        var selfCount = currentEntry.Count;
        var result = new List<ResultField>();
        if (!string.IsNullOrEmpty(path)) path += '\x1e';
        long totalTime = 0;
        foreach (var key in _entries.Keys)
        {
            if (IsDirectChild(path, key)) totalTime += GetEntry(key).Duration;
        }
        var oldTime = totalTime;
        if (totalTime < selfTime) totalTime = selfTime;
        if (globalTime < totalTime) globalTime = totalTime;
        foreach (var key2 in _entries.Keys)
        {
            if (IsDirectChild(path, key2))
            {
                var entry = GetEntry(key2);
                var time = entry.Duration;
                var timePercentage = (time * 100.0d) / totalTime;
                var globalPercentage = (time * 100.0d) / globalTime;
                var name = key2.Substring(path.Length);
                result.Add(new ResultField(name, timePercentage, globalPercentage, entry.Count));
            }
        }
        if (totalTime > oldTime)
        {
            result.Add(new ResultField("unspecified", ((totalTime - oldTime) * 100.0d) / totalTime, ((totalTime - oldTime) * 100.0d) / globalTime, selfCount));
        }
        result.Sort();
        result.Insert(0, new ResultField(path, 100.0d, (totalTime * 100.0d) / globalTime, selfCount));
        return result;
    }

    //直接子路径判断对应原版isDirectChild
    private static bool IsDirectChild(string path, string test)
        => test.Length > path.Length
            && test.StartsWith(path, StringComparison.Ordinal)
            && test.IndexOf('\x1e', path.Length + 1) < 0;

    public long StartTimeNano => _startTimeNano;
    public int StartTimeTicks => _startTimeTicks;
    public long EndTimeNano => _endTimeNano;
    public int EndTimeTicks => _endTimeTicks;
    public int TickDuration => _tickDuration;
    public long NanoDuration => _endTimeNano - _startTimeNano;

    public bool SaveResults(string file)
    {
        try
        {
            var dir = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(file, GetProfilerResults(NanoDuration, _tickDuration));
            return true;
        }
        catch (Exception e)
        {
            Log.Error($"Could not save profiler results to {file}: {e}");
            return false;
        }
    }

    //完整结果文本对应原版getProfilerResults(timespan, tickspan)
    //包含header/version/time span/tick span/profile dump/counter dump
    public string GetProfilerResults(long timespan, int tickspan)
    {
        var builder = new StringBuilder();
        builder.Append("Version: ").Append(SharedConstants.Version).Append('\n');
        builder.Append("Time span: ").Append(timespan / ProfilingUtil.NanosPerMilli).Append(" ms\n");
        builder.Append("Tick span: ").Append(tickspan).Append(" ticks\n");
        var tps = tickspan / (timespan / 1.0E9f);
        builder.Append("// This is approximately ").Append(tps.ToString("F2", CultureInfo.InvariantCulture)).Append(" ticks per second. It should be ").Append(20).Append(" ticks per second\n\n");
        builder.Append("--- BEGIN PROFILE DUMP ---\n\n");
        AppendProfilerResults(0, ProfilerFiller.Root, builder);
        builder.Append("--- END PROFILE DUMP ---\n\n");
        var counters = GetCounterValues();
        if (counters.Count > 0)
        {
            builder.Append("--- BEGIN COUNTER DUMP ---\n\n");
            AppendCounters(counters, builder, tickspan);
            builder.Append("--- END COUNTER DUMP ---\n\n");
        }
        return builder.ToString();
    }

    public string GetProfilerResults()
    {
        var builder = new StringBuilder();
        AppendProfilerResults(0, ProfilerFiller.Root, builder);
        return builder.ToString();
    }

    private static StringBuilder IndentLine(StringBuilder builder, int depth)
    {
        builder.Append(string.Format(CultureInfo.InvariantCulture, "[{0:00}] ", depth));
        for (var j = 0; j < depth; j++) builder.Append("|   ");
        return builder;
    }

    private void AppendProfilerResults(int depth, string path, StringBuilder builder)
    {
        var results = GetTimes(path);
        var entry = _entries.TryGetValue(path, out var e) ? e : EmptyEntry;
        foreach (var (id, value) in entry.Counters)
        {
            IndentLine(builder, depth).Append('#').Append(id).Append(' ').Append(value).Append('/').Append(value / _tickDuration).Append('\n');
        }
        if (results.Count < 3) return;
        for (var i = 1; i < results.Count; i++)
        {
            var result = results[i];
            IndentLine(builder, depth)
                .Append(result.Name).Append('(').Append(result.Count).Append('/')
                .Append(string.Format(CultureInfo.InvariantCulture, "{0:F0}", (float)result.Count / _tickDuration)).Append(')')
                .Append(" - ").Append(string.Format(CultureInfo.InvariantCulture, "{0:F2}", result.Percentage)).Append("%/")
                .Append(string.Format(CultureInfo.InvariantCulture, "{0:F2}", result.GlobalPercentage)).Append("%\n");
            if (!"unspecified".Equals(result.Name))
            {
                try
                {
                    AppendProfilerResults(depth + 1, path + '\x1e' + result.Name, builder);
                }
                catch (Exception ex)
                {
                    builder.Append("[[ EXCEPTION ").Append(ex).Append(" ]]");
                }
            }
        }
    }

    private void AppendCounterResults(int depth, string name, CounterCollector result, int tickspan, StringBuilder builder)
    {
        IndentLine(builder, depth).Append(name).Append(" total:").Append(result.SelfValue).Append('/').Append(result.TotalValue)
            .Append(" average: ").Append(result.SelfValue / tickspan).Append('/').Append(result.TotalValue / tickspan).Append('\n');
        foreach (var (childName, child) in result.Children.OrderByDescending(kv => kv.Value.TotalValue))
        {
            AppendCounterResults(depth + 1, childName, child, tickspan, builder);
        }
    }

    private void AppendCounters(IReadOnlyDictionary<string, CounterCollector> counters, StringBuilder builder, int tickspan)
    {
        foreach (var (counter, counterRoot) in counters)
        {
            builder.Append("-- Counter: ").Append(counter).Append(" --\n");
            if (counterRoot.Children.TryGetValue(ProfilerFiller.Root, out var rootChild))
            {
                AppendCounterResults(0, ProfilerFiller.Root, rootChild, tickspan, builder);
            }
            builder.Append("\n\n");
        }
    }

    //收集所有counter按名字聚合到树对应原版getCounterValues
    private IReadOnlyDictionary<string, CounterCollector> GetCounterValues()
    {
        var result = new SortedDictionary<string, CounterCollector>();
        foreach (var (path, entry) in _entries)
        {
            if (entry.Counters.Count == 0) continue;
            var pathSegments = path.Split('\x1e');
            foreach (var (counter, value) in entry.Counters)
            {
                if (!result.TryGetValue(counter, out var collector))
                {
                    collector = new CounterCollector();
                    result[counter] = collector;
                }
                collector.AddValue(pathSegments, 0, value);
            }
        }
        return result;
    }

    //空PathEntry单例对应原版EMPTY
    private sealed class EmptyProfilerPathEntry : ProfilerPathEntry
    {
        public long Duration => 0L;
        public long MaxDuration => 0L;
        public long Count => 0L;
        public IReadOnlyDictionary<string, long> Counters => new Dictionary<string, long>();
    }

    //counter聚合器对应原版CounterCollector
    //按路径段构造树selfValue为叶子totalValue为子树和
    private sealed class CounterCollector
    {
        public long SelfValue;
        public long TotalValue;
        public readonly Dictionary<string, CounterCollector> Children = new();

        public void AddValue(string[] pathSegments, int index, long value)
        {
            TotalValue += value;
            if (index >= pathSegments.Length)
            {
                SelfValue += value;
            }
            else
            {
                var segment = pathSegments[index];
                if (!Children.TryGetValue(segment, out var child))
                {
                    child = new CounterCollector();
                    Children[segment] = child;
                }
                child.AddValue(pathSegments, index + 1, value);
            }
        }
    }
}
