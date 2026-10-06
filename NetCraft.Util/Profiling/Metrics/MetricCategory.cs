namespace NetCraft.Util.Profiling.Metrics;

//Metric category enum, maps to vanilla net.minecraft.util.profiling.metrics.MetricCategory
//Profiler samplers aggregate by this category
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

//MetricCategory extension, maps to vanilla getDescription
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
