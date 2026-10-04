namespace NetCraft.Game.Util.DebugChart;

//LocalSampleLogger 服务端本地采样环形缓冲 对应原版 net.minecraft.util.debugchart.LocalSampleLogger
//固定保留最近 240 帧 写满从头覆盖 供 /debug 图表读取
public class LocalSampleLogger : AbstractSampleLogger, SampleStorage
{
    public const int CapacityLimit = 240;
    private readonly long[][] _samples;
    private int _start;
    private int _size;

    public LocalSampleLogger(int dimensions) : this(dimensions, new long[dimensions])
    {
    }

    public LocalSampleLogger(int dimensions, long[] defaults) : base(dimensions, defaults)
    {
        _samples = new long[CapacityLimit][];
        for (var i = 0; i < CapacityLimit; i++)
        {
            _samples[i] = new long[dimensions];
        }
    }

    protected override void UseSample()
    {
        var nextIndex = WrapIndex(_start + _size);
        Array.Copy(Sample, 0, _samples[nextIndex], 0, Sample.Length);
        if (_size < CapacityLimit)
        {
            _size++;
        }
        else
        {
            _start = WrapIndex(_start + 1);
        }
    }

    public int Capacity => _samples.Length;

    public int Size => _size;

    public long Get(int index) => Get(index, 0);

    public long Get(int index, int dimension)
    {
        if (index < 0 || index >= _size)
        {
            throw new IndexOutOfRangeException($"{index} out of bounds for length {_size}");
        }
        var sample = _samples[WrapIndex(_start + index)];
        if (dimension < 0 || dimension >= sample.Length)
        {
            throw new IndexOutOfRangeException($"{dimension} out of bounds for dimensions {sample.Length}");
        }
        return sample[dimension];
    }

    public void Reset()
    {
        _start = 0;
        _size = 0;
    }

    private static int WrapIndex(int index) => index % CapacityLimit;
}
