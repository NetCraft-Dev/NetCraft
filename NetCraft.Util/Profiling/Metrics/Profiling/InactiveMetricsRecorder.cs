using NetCraft.Util.Profiling;

namespace NetCraft.Util.Profiling.Metrics.Profiling;

//Inactive metrics recorder, maps to vanilla net.minecraft.util.profiling.metrics.profiling.InactiveMetricsRecorder
//Singleton with all methods empty
public sealed class InactiveMetricsRecorder : MetricsRecorder
{
    public static readonly MetricsRecorder Instance = new InactiveMetricsRecorder();

    private InactiveMetricsRecorder() { }

    public void End() { }
    public void Cancel() { }
    public void StartTick() { }
    public void SampleDuringExtract() { }
    public bool IsRecording() => false;
    public ProfilerFiller GetProfiler() => InactiveProfiler.Instance;
    public void EndTick() { }
}
