using NetCraft.Util.Profiling;
using NetCraft.Util.Profiling.Metrics;

namespace NetCraft.Util.Profiling.Metrics.Profiling;

//profiler采样器适配器对应原版net.minecraft.util.profiling.metrics.profiling.ProfilerSamplerAdapter
//从chartedPaths中扫描新路径创建MetricSampler
public sealed class ProfilerSamplerAdapter
{
    private readonly HashSet<string> _previouslyFoundSamplerNames = new();

    public ISet<MetricSampler> NewSamplersFoundInProfiler(Func<ProfileCollector> profiler)
    {
        var newSamplers = new HashSet<MetricSampler>();
        foreach (var (path, category) in profiler().GetChartedPaths())
        {
            if (_previouslyFoundSamplerNames.Contains(path)) continue;
            var sampler = SamplerForProfilingPath(profiler, path, category);
            newSamplers.Add(sampler);
            _previouslyFoundSamplerNames.Add(sampler.Name);
        }
        return newSamplers;
    }

    private static MetricSampler SamplerForProfilingPath(Func<ProfileCollector> profiler, string profilerPath, MetricCategory category)
        => MetricSampler.Create(profilerPath, category, () =>
        {
            var entry = profiler().GetEntry(profilerPath);
            if (entry is null) return 0.0d;
            return entry.MaxDuration / (double)ProfilingUtil.NanosecondsPerMillisecond;
        });
}
