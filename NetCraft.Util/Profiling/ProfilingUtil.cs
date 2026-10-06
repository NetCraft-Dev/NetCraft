using System.Diagnostics;

namespace NetCraft.Util.Profiling;

//profiling subdomain inlined time helpers
//Maps to vanilla Util.getNanos/Util.NANOS_PER_MILLI/Util.timeSource/Util.getFilenameFormattedDateTime/TimeUtil.NANOSECONDS_PER_MILLISECOND
//The whole Util/TimeUtil classes are not ported; only the minimal set profiling needs is inlined here
public static class ProfilingUtil
{
    public const long NanosPerMilli = 1_000_000L;

    public const long NanosecondsPerMillisecond = 1_000_000L;

    private static readonly double TicksToNanos = 1_000_000_000.0d / Stopwatch.Frequency;

    //Maps to vanilla Util.getNanos, returns a high-precision nanosecond timestamp
    public static long GetNanos() => (long)(Stopwatch.GetTimestamp() * TicksToNanos);

    //Maps to vanilla Util.timeSource, a LongSupplier returning nanosecond timestamps
    public static long TimeSource() => GetNanos();

    //Maps to vanilla Util.getFilenameFormattedDateTime, generates a filename-safe date-time string
    public static string GetFilenameFormattedDateTime()
        => DateTimeOffset.Now.ToString("yyyy-MM-dd_HH.mm.ss");
}
