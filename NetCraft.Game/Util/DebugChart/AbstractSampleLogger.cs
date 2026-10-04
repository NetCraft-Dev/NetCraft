namespace NetCraft.Game.Util.DebugChart;

//AbstractSampleLogger 采样缓冲基类 对应原版 net.minecraft.util.debugchart.AbstractSampleLogger
//子类实现 UseSample 决定样本写向哪里 每帧消费完按默认值复位
public abstract class AbstractSampleLogger : SampleLogger
{
    protected readonly long[] Defaults;
    protected readonly long[] Sample;

    protected AbstractSampleLogger(int dimensions, long[] defaults)
    {
        if (defaults.Length != dimensions)
        {
            throw new ArgumentException($"defaults have incorrect length of {defaults.Length}");
        }
        Sample = new long[dimensions];
        Defaults = defaults;
    }

    public void LogFullSample(long[] sample)
    {
        Array.Copy(sample, 0, Sample, 0, sample.Length);
        UseSample();
        ResetSample();
    }

    public void LogSample(long sample)
    {
        Sample[0] = sample;
        UseSample();
        ResetSample();
    }

    public void LogPartialSample(long sample, int dimension)
    {
        if (dimension < 1 || dimension >= Sample.Length)
        {
            throw new IndexOutOfRangeException($"{dimension} out of bounds for dimensions {Sample.Length}");
        }
        Sample[dimension] = sample;
    }

    //UseSample 把当前样本写入目标存储 由子类决定去处
    protected abstract void UseSample();

    //ResetSample 还原默认值等待下一帧
    protected void ResetSample() => Array.Copy(Defaults, 0, Sample, 0, Defaults.Length);
}
