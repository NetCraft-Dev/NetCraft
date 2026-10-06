using NetCraft.Util.Profiling;

namespace NetCraft.Util.Profiling.Metrics;

//Sampler provider interface, maps to vanilla net.minecraft.util.profiling.metrics.MetricsSamplerProvider
//Returns a set of MetricSampler for use by MetricsRecorder
public interface MetricsSamplerProvider
{
    ISet<MetricSampler> Samplers(Func<ProfileCollector> singleTickProfiler);
}
