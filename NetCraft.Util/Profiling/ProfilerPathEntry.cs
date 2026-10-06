namespace NetCraft.Util.Profiling;

//Profiler path entry interface, maps to vanilla net.minecraft.util.profiling.ProfilerPathEntry
//Provides path timing statistics accessors
public interface ProfilerPathEntry
{
    long Duration { get; }

    long MaxDuration { get; }

    long Count { get; }

    IReadOnlyDictionary<string, long> Counters { get; }
}
