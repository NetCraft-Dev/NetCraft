namespace NetCraft.Game.Util.DebugChart;

//SampleLogger debug sample recorder, maps to vanilla net.minecraft.util.debugchart.SampleLogger
//LogFullSample logs a full frame; LogSample logs only the first dimension; LogPartialSample logs the given dimension
public interface SampleLogger
{
    void LogFullSample(long[] sample);

    void LogSample(long sample);

    void LogPartialSample(long sample, int dimension);
}
