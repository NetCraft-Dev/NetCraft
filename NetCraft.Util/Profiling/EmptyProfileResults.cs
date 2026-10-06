namespace NetCraft.Util.Profiling;

//Empty profiler results, maps to vanilla net.minecraft.util.profiling.EmptyProfileResults
//All methods return empty/zero
public sealed class EmptyProfileResults : ProfileResults
{
    public static readonly EmptyProfileResults Empty = new();

    private EmptyProfileResults() { }

    public List<ResultField> GetTimes(string path) => new();
    public bool SaveResults(string file) => false;
    public long StartTimeNano => 0L;
    public int StartTimeTicks => 0;
    public long EndTimeNano => 0L;
    public int EndTimeTicks => 0;
    public string GetProfilerResults() => string.Empty;
}
