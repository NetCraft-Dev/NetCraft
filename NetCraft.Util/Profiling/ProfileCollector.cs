using NetCraft.Codec;
using NetCraft.Util.Profiling.Metrics;

namespace NetCraft.Util.Profiling;

//profile收集器接口对应原版net.minecraft.util.profiling.ProfileCollector
//继承ProfilerFiller并扩展获取结果/路径条目/图表化路径
public interface ProfileCollector : ProfilerFiller
{
    ProfileResults GetResults();

    ProfilerPathEntry? GetEntry(string path);

    ISet<Pair<string, MetricCategory>> GetChartedPaths();
}
