using System.Runtime.CompilerServices;

namespace NetCraft.Util.Profiling.Metrics;

//指标注册表对应原版net.minecraft.util.profiling.metrics.MetricsRegistry
//WeakReference管理ProfilerMeasured实例聚合同名采样器
public sealed class MetricsRegistry
{
    public static readonly MetricsRegistry Instance = new();

    private readonly ConditionalWeakTable<ProfilerMeasured, object> _measuredInstances = new();

    public MetricsRegistry() { }

    public void Add(ProfilerMeasured profilerMeasured)
        => _measuredInstances.Add(profilerMeasured, new object());

    public List<MetricSampler> GetRegisteredSamplers()
    {
        var samplersByName = new Dictionary<string, List<MetricSampler>>();
        //ConditionalWeakTable无直接枚举API需要通过弱引用快照
        foreach (var measured in SnapshotMeasured())
        {
            foreach (var sampler in measured.ProfiledMetrics())
            {
                if (!samplersByName.TryGetValue(sampler.Name, out var list))
                {
                    list = new List<MetricSampler>();
                    samplersByName[sampler.Name] = list;
                }
                list.Add(sampler);
            }
        }
        return AggregateDuplicates(samplersByName);
    }

    private List<ProfilerMeasured> SnapshotMeasured()
    {
        //ConditionalWeakTable.NET 10支持Enumerate若不可用返回空列表
        var result = new List<ProfilerMeasured>();
        try
        {
            foreach (var (key, _) in _measuredInstances)
            {
                if (key is not null) result.Add(key);
            }
        }
        catch
        {
            //Enumerate不可用或失败时返回空
        }
        return result;
    }

    private static List<MetricSampler> AggregateDuplicates(Dictionary<string, List<MetricSampler>> potentialDuplicates)
    {
        var result = new List<MetricSampler>();
        foreach (var (name, duplicates) in potentialDuplicates)
        {
            result.Add(duplicates.Count > 1
                ? new AggregatedMetricSampler(name, duplicates)
                : duplicates[0]);
        }
        return result;
    }

    //聚合采样器对应原版MetricsRegistry.AggregatedMetricSampler
    //多个同名采样器求平均值
    internal sealed class AggregatedMetricSampler : MetricSampler
    {
        private readonly List<MetricSampler> _delegates;

        internal AggregatedMetricSampler(string name, List<MetricSampler> delegates)
            : base(name, SamplingPhase.EndTick, delegates[0].Category,
                   () => AverageValueFromDelegates(delegates),
                   () => BeforeTick(delegates),
                   ThresholdTestFor(delegates))
        {
            _delegates = delegates;
        }

        private static MetricSampler.ThresholdTest? ThresholdTestFor(List<MetricSampler> delegates)
            => delegates.Any(d => d._thresholdTest is not null)
                ? new AggregateThresholdTest(delegates)
                : null;

        private static void BeforeTick(List<MetricSampler> delegates)
        {
            foreach (var d in delegates) d.OnStartTick();
        }

        private static double AverageValueFromDelegates(List<MetricSampler> delegates)
        {
            var sum = 0.0d;
            foreach (var d in delegates) sum += d.Sampler();
            return sum / delegates.Count;
        }
    }

    //聚合阈值测试任一子采样器触发即触发
    private sealed class AggregateThresholdTest : MetricSampler.ThresholdTest
    {
        private readonly List<MetricSampler> _delegates;

        public AggregateThresholdTest(List<MetricSampler> delegates) => _delegates = delegates;

        public bool Test(double value)
        {
            foreach (var d in _delegates)
            {
                if (d._thresholdTest is { } test && test.Test(value)) return true;
            }
            return false;
        }
    }
}
