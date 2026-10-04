namespace NetCraft.Util.Profiling.Metrics;

//指标分类枚举对应原版net.minecraft.util.profiling.metrics.MetricCategory
//profiler采样器按此分类聚合
public enum MetricCategory
{
    PathFinding,
    EventLoops,
    ConsecutiveExecutors,
    TickLoop,
    Jvm,
    ChunkRendering,
    ChunkRenderingDispatching,
    Cpu,
    Gpu
}

//MetricCategory扩展对应原版getDescription
public static class MetricCategoryExtensions
{
    public static string GetDescription(this MetricCategory category) => category switch
    {
        MetricCategory.PathFinding => "pathfinding",
        MetricCategory.EventLoops => "event-loops",
        MetricCategory.ConsecutiveExecutors => "consecutive-executors",
        MetricCategory.TickLoop => "ticking",
        MetricCategory.Jvm => "jvm",
        MetricCategory.ChunkRendering => "chunk rendering",
        MetricCategory.ChunkRenderingDispatching => "chunk rendering dispatching",
        MetricCategory.Cpu => "cpu",
        MetricCategory.Gpu => "gpu",
        _ => category.ToString().ToLowerInvariant()
    };
}
