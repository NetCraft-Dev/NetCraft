namespace NetCraft.Game.Util.DebugChart;

//SampleLogger 调试采样记录器 对应原版 net.minecraft.util.debugchart.SampleLogger
//LogFullSample 记完整一帧 LogSample 只记首维 LogPartialSample 记指定维
public interface SampleLogger
{
    void LogFullSample(long[] sample);

    void LogSample(long sample);

    void LogPartialSample(long sample, int dimension);
}
