namespace NetCraft.Util.Profiling;

//profiler结果接口对应原版net.minecraft.util.profiling.ProfileResults
//提供路径耗时查询与结果持久化
public interface ProfileResults
{
    public const char PathSeparator = '\x1e';

    List<ResultField> GetTimes(string path);

    bool SaveResults(string file);

    long StartTimeNano { get; }

    int StartTimeTicks { get; }

    long EndTimeNano { get; }

    int EndTimeTicks { get; }

    string GetProfilerResults();

    //总纳秒耗时对应原版getNanoDuration
    public long NanoDuration => EndTimeNano - StartTimeNano;

    //总tick跨度对应原版getTickDuration
    public int TickDuration => EndTimeTicks - StartTimeTicks;

    //路径美化对应原版demanglePath把分隔符替换为点
    public static string DemanglePath(string path) => path.Replace('\x1e', '.');
}
