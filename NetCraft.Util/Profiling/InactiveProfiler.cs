using NetCraft.Codec;
using NetCraft.Util.Profiling.Metrics;

namespace NetCraft.Util.Profiling;

//非活跃profiler对应原版net.minecraft.util.profiling.InactiveProfiler
//所有方法空实现单例
public sealed class InactiveProfiler : ProfileCollector
{
    public static readonly InactiveProfiler Instance = new();

    private InactiveProfiler() { }

    public void StartTick() { }
    public void EndTick() { }
    public void Push(string name) { }
    public void Push(Func<string> name) { }
    public void MarkForCharting(MetricCategory category) { }
    public void Pop() { }
    public void PopPush(string name) { }
    public void PopPush(Func<string> name) { }
    public void IncrementCounter(string name, int amount) { }
    public void IncrementCounter(Func<string> name, int amount) { }

    //Zone覆盖返回Inactive单例避免无谓对象分配
    //接口默认实现会new Zone(this)此处用new关键字显式覆盖
    //方法名遮蔽Zone类名用global前缀引用Zone类
    public new global::NetCraft.Util.Profiling.Zone Zone(string name) => global::NetCraft.Util.Profiling.Zone.Inactive;
    public new global::NetCraft.Util.Profiling.Zone Zone(Func<string> name) => global::NetCraft.Util.Profiling.Zone.Inactive;

    public ProfileResults GetResults() => EmptyProfileResults.Empty;
    public ProfilerPathEntry? GetEntry(string path) => null;
    public ISet<Pair<string, MetricCategory>> GetChartedPaths() => new HashSet<Pair<string, MetricCategory>>();
}
