namespace NetCraft.Game.Util.DebugChart;

//SampleStorage 采样缓冲只读视图 对应原版 net.minecraft.util.debugchart.SampleStorage
public interface SampleStorage
{
    int Capacity { get; }

    int Size { get; }

    long Get(int index);

    long Get(int index, int dimension);

    void Reset();
}
