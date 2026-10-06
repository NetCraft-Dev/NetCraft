using NetCraft.Codec;
using NetCraft.Util.Profiling.Metrics;

namespace NetCraft.Util.Profiling;

//Inactive profiler, maps to vanilla net.minecraft.util.profiling.InactiveProfiler
//Singleton with all methods empty
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

    //Zone override returns the Inactive singleton to avoid needless allocation
    //The interface default would new Zone(this); this explicitly overrides with the new keyword
    //Method name shadows the Zone class name, so the global prefix refers to the Zone class
    public new global::NetCraft.Util.Profiling.Zone Zone(string name) => global::NetCraft.Util.Profiling.Zone.Inactive;
    public new global::NetCraft.Util.Profiling.Zone Zone(Func<string> name) => global::NetCraft.Util.Profiling.Zone.Inactive;

    public ProfileResults GetResults() => EmptyProfileResults.Empty;
    public ProfilerPathEntry? GetEntry(string path) => null;
    public ISet<Pair<string, MetricCategory>> GetChartedPaths() => new HashSet<Pair<string, MetricCategory>>();
}
