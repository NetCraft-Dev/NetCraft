namespace NetCraft.Game.Util.DebugChart;

//SampleStorage read-only view over the sample buffer, maps to vanilla net.minecraft.util.debugchart.SampleStorage
public interface SampleStorage
{
    int Capacity { get; }

    int Size { get; }

    long Get(int index);

    long Get(int index, int dimension);

    void Reset();
}
