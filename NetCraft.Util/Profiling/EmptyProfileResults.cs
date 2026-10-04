namespace NetCraft.Util.Profiling;

//空profiler结果对应原版net.minecraft.util.profiling.EmptyProfileResults
//所有方法返回空/零
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
