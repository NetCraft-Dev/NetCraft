namespace NetCraft.Util.Profiling;

//Profiler results interface, maps to vanilla net.minecraft.util.profiling.ProfileResults
//Provides path timing queries and result persistence
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

    //Total nanosecond duration, maps to vanilla getNanoDuration
    public long NanoDuration => EndTimeNano - StartTimeNano;

    //Total tick span, maps to vanilla getTickDuration
    public int TickDuration => EndTimeTicks - StartTimeTicks;

    //Path prettifying, maps to vanilla demanglePath replacing separators with dots
    public static string DemanglePath(string path) => path.Replace('\x1e', '.');
}
