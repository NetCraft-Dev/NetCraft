using NetCraft.Config;

namespace NetCraft.Optimizations.Profiler;

//Profiler optimization module, covers core optimization point 2.12
//The actual optimization lands once the NetCraft.Util/Profiler subsystem is ready
//Currently only exposes the toggle query API, aligned with the vanilla ProfilerFiller string concatenation path
public static class ProfilerOptimizations
{
    public const string ModuleName = "Profiler Optimization";
    public const string TargetSubsystem = "NetCraft.Util (Profiler)";

    //Optimization point 2.12, profiler paths use stack allocated Span<char>
    //When the toggle is on, once the Profiler subsystem is ready it uses Span<char> instead of string concatenation
    public static bool IsSpanPathEnabled => OptimizationFlags.ProfilerSpanPath;

    //Optimization point 2.12, profiler uses a zero-allocation InterpolatedStringHandler
    //When the toggle is on, the push/pop path uses InterpolatedStringHandler to avoid string allocations
    public static bool IsZeroAllocEnabled => OptimizationFlags.ProfilerZeroAlloc;

    //IsOptimized checks whether both toggles are on to decide if Profiler optimization is enabled
    public static bool IsOptimized => IsSpanPathEnabled && IsZeroAllocEnabled;

    //GetStats returns the Profiler optimization stats for diagnostics
    public static ProfilerOptimizationStats GetStats() => new(
        SpanPath: IsSpanPathEnabled,
        ZeroAlloc: IsZeroAllocEnabled,
        IsOptimized: IsOptimized);
}

//Profiler optimization stats snapshot
public readonly record struct ProfilerOptimizationStats(
    bool SpanPath,
    bool ZeroAlloc,
    bool IsOptimized);
