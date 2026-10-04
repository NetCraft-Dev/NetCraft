using System.Diagnostics;

namespace NetCraft.Util.Profiling;

//profiling 子领域内联时间工具
//对应原版Util.getNanos/Util.NANOS_PER_MILLI/Util.timeSource/Util.getFilenameFormattedDateTime/TimeUtil.NANOSECONDS_PER_MILLISECOND
//Util/TimeUtil整类未移植此处仅内联profiling需要的最小集
public static class ProfilingUtil
{
    public const long NanosPerMilli = 1_000_000L;

    public const long NanosecondsPerMillisecond = 1_000_000L;

    private static readonly double TicksToNanos = 1_000_000_000.0d / Stopwatch.Frequency;

    //对应原版Util.getNanos返回高精度纳秒时间戳
    public static long GetNanos() => (long)(Stopwatch.GetTimestamp() * TicksToNanos);

    //对应原版Util.timeSource返回纳秒时间戳的LongSupplier
    public static long TimeSource() => GetNanos();

    //对应原版Util.getFilenameFormattedDateTime生成文件名安全的日期时间字符串
    public static string GetFilenameFormattedDateTime()
        => DateTimeOffset.Now.ToString("yyyy-MM-dd_HH.mm.ss");
}
