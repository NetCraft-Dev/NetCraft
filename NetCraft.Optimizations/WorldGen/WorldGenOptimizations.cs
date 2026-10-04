using NetCraft.Config;

namespace NetCraft.Optimizations.WorldGen;

//WorldGen 优化模块对应核心优化点 2.14
//2.14 区块生成多核并行调度集成于 NetCraft.Storage/Level/ServerChunkCache.cs 的生成闸门
//借鉴 C2ME Concurrent Chunk Management Engine 方案
public static class WorldGenOptimizations
{
    public const string ModuleName = "World Generation Optimization";
    public const string TargetSubsystem = "NetCraft.Game.World.Level.LevelGen";

    //对应优化点 2.14 区块生成并发度按处理器核数 关闭时退化为单线程串行
    public static bool IsChunkGenerationParallelEnabled => OptimizationFlags.ChunkGenerationParallel;

    //GetGenerationParallelism 返回实际生成并发度 开关关闭时为 1 保证串行基线可对照
    public static int GetGenerationParallelism()
        => IsChunkGenerationParallelEnabled ? Math.Max(1, Environment.ProcessorCount) : 1;

    //IsOptimized 检查区块生成并行是否启用
    public static bool IsOptimized => IsChunkGenerationParallelEnabled;

    //GetStats 返回 WorldGen 优化统计信息用于诊断
    public static WorldGenOptimizationStats GetStats() => new(
        ChunkGenerationParallel: IsChunkGenerationParallelEnabled,
        GenerationParallelism: GetGenerationParallelism(),
        IsOptimized: IsOptimized);
}

//WorldGen 优化统计快照
public readonly record struct WorldGenOptimizationStats(
    bool ChunkGenerationParallel,
    int GenerationParallelism,
    bool IsOptimized);
