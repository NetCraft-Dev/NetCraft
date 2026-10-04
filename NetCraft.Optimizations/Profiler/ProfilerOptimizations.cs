using NetCraft.Config;

namespace NetCraft.Optimizations.Profiler;

//Profiler 优化模块对应核心优化点 2.12
//实际优化待 NetCraft.Util/Profiler 子系统就绪后集成
//当前仅暴露开关查询 API 对齐原版 ProfilerFiller 字符串拼接路径
public static class ProfilerOptimizations
{
    public const string ModuleName = "Profiler Optimization";
    public const string TargetSubsystem = "NetCraft.Util (Profiler)";

    //对应优化点 2.12 Profiler 路径用 Span<char> 栈分配
    //开关启用表示 Profiler 子系统就绪后将采用 Span<char> 替代 string 拼接
    public static bool IsSpanPathEnabled => OptimizationFlags.ProfilerSpanPath;

    //对应优化点 2.12 Profiler 用 InterpolatedStringHandler 零分配
    //开关启用表示 push/pop 路径将采用 InterpolatedStringHandler 避免 string 分配
    public static bool IsZeroAllocEnabled => OptimizationFlags.ProfilerZeroAlloc;

    //IsOptimized 检查两开关是否全开判断 Profiler 优化是否启用
    public static bool IsOptimized => IsSpanPathEnabled && IsZeroAllocEnabled;

    //GetStats 返回 Profiler 优化统计信息用于诊断
    public static ProfilerOptimizationStats GetStats() => new(
        SpanPath: IsSpanPathEnabled,
        ZeroAlloc: IsZeroAllocEnabled,
        IsOptimized: IsOptimized);
}

//Profiler 优化统计快照
public readonly record struct ProfilerOptimizationStats(
    bool SpanPath,
    bool ZeroAlloc,
    bool IsOptimized);
