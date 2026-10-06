namespace NetCraft.Util.Profiling.Metrics;

//Profiler-measurable object interface, maps to vanilla net.minecraft.util.profiling.metrics.ProfilerMeasured
//Implementations return their own MetricSampler list, aggregated by MetricsRegistry
public interface ProfilerMeasured
{
    List<MetricSampler> ProfiledMetrics();
}
