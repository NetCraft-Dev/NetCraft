using NetCraft.Util.Profiling;

namespace NetCraft.Util.Profiling.Metrics;

//采样器提供者接口对应原版net.minecraft.util.profiling.metrics.MetricsSamplerProvider
//返回一组MetricSampler供MetricsRecorder使用
public interface MetricsSamplerProvider
{
    ISet<MetricSampler> Samplers(Func<ProfileCollector> singleTickProfiler);
}
