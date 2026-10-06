using NetCraft.Util.Profiling;

namespace NetCraft.Util.Profiling.Metrics.Profiling;

//Metrics recorder interface, maps to vanilla net.minecraft.util.profiling.metrics.profiling.MetricsRecorder
//Controls sampling window start/stop/tick sampling
public interface MetricsRecorder
{
    void End();

    void Cancel();

    void StartTick();

    void SampleDuringExtract();

    bool IsRecording();

    ProfilerFiller GetProfiler();

    void EndTick();
}
