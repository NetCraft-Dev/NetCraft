namespace NetCraft.Util.Profiling;

//Profiler zone, maps to vanilla net.minecraft.util.profiling.Zone
//AutoCloseable wrapping a push/pop scope, can attach text/value/color
public sealed class Zone : IDisposable
{
    public static readonly Zone Inactive = new(null!);

    private readonly ProfilerFiller? _profiler;

    internal Zone(ProfilerFiller? profiler)
    {
        _profiler = profiler;
    }

    public Zone AddText(string text)
    {
        _profiler?.AddZoneText(text);
        return this;
    }

    public Zone AddText(Func<string> text)
    {
        _profiler?.AddZoneText(text());
        return this;
    }

    public Zone AddValue(long value)
    {
        _profiler?.AddZoneValue(value);
        return this;
    }

    public Zone SetColor(int color)
    {
        _profiler?.SetZoneColor(color);
        return this;
    }

    public void Dispose()
    {
        _profiler?.Pop();
    }
}
