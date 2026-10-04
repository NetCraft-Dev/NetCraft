using NetCraft.Util.Profiling;

namespace NetCraft.Util.Profiling.Metrics.Profiling;

//指标记录器接口对应原版net.minecraft.util.profiling.metrics.profiling.MetricsRecorder
//控制采样窗口的启动/停止/tick采样
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
