using NetCraft.Util.Profiling;

namespace NetCraft.Util.Profiling.Metrics.Profiling;

//非活跃指标记录器对应原版net.minecraft.util.profiling.metrics.profiling.InactiveMetricsRecorder
//所有方法空实现单例
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
