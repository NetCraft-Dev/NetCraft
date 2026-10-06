namespace NetCraft.Game.Util.DebugChart;

//AbstractSampleLogger sample buffer base class, maps to vanilla net.minecraft.util.debugchart.AbstractSampleLogger
//Subclasses implement UseSample to decide where samples go; reset to defaults after each frame is consumed
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

    //UseSample writes the current sample to the target storage; subclasses decide the destination
    protected abstract void UseSample();

    //ResetSample restores defaults and waits for the next frame
    protected void ResetSample() => Array.Copy(Defaults, 0, Sample, 0, Defaults.Length);
}
