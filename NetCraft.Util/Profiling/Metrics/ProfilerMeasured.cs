namespace NetCraft.Util.Profiling.Metrics;

//可被profiler测量的对象接口对应原版net.minecraft.util.profiling.metrics.ProfilerMeasured
//实现类返回自己的MetricSampler列表由MetricsRegistry聚合
public interface ProfilerMeasured
{
    List<MetricSampler> ProfiledMetrics();
}
