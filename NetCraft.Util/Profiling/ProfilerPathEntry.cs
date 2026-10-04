namespace NetCraft.Util.Profiling;

//profiler路径条目接口对应原版net.minecraft.util.profiling.ProfilerPathEntry
//提供路径耗时统计读取方法
public interface ProfilerPathEntry
{
    long Duration { get; }

    long MaxDuration { get; }

    long Count { get; }

    IReadOnlyDictionary<string, long> Counters { get; }
}
