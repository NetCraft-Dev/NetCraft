using NetCraft.Util.Profiling;

namespace NetCraft.Util.Profiling.Metrics.Storage;

//Recorded deviation, maps to vanilla net.minecraft.util.profiling.metrics.storage.RecordedDeviation
//Saves the timestamp/tick/profiler results at the time when a sampler triggers its threshold
public sealed class RecordedDeviation
{
    public DateTimeOffset Timestamp { get; }
    public int Tick { get; }
    public ProfileResults ProfilerResultAtTick { get; }

    public RecordedDeviation(DateTimeOffset timestamp, int tick, ProfileResults profilerResultAtTick)
    {
        Timestamp = timestamp;
        Tick = tick;
        ProfilerResultAtTick = profilerResultAtTick;
    }
}
