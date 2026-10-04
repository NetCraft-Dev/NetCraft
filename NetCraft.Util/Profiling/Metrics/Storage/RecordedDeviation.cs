using NetCraft.Util.Profiling;

namespace NetCraft.Util.Profiling.Metrics.Storage;

//偏差记录对应原版net.minecraft.util.profiling.metrics.storage.RecordedDeviation
//采样器触发阈值时保存时间戳/tick/当时的profiler结果
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
