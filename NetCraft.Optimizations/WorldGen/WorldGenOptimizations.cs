using NetCraft.Config;

namespace NetCraft.Optimizations.WorldGen;

//WorldGen optimization module, covers core optimization point 2.14
//2.14 Multicore parallel scheduling for chunk generation is integrated in the generation gate in NetCraft.Storage/Level/ServerChunkCache.cs
//Borrows from the C2ME Concurrent Chunk Management Engine approach
public static class WorldGenOptimizations
{
    public const string ModuleName = "World Generation Optimization";
    public const string TargetSubsystem = "NetCraft.Game.World.Level.LevelGen";

    //Optimization point 2.14, chunk generation concurrency follows the core count, single-threaded serial when off
    public static bool IsChunkGenerationParallelEnabled => OptimizationFlags.ChunkGenerationParallel;

    //GetGenerationParallelism returns the actual generation parallelism, 1 when the toggle is off to keep a serial baseline for comparison
    public static int GetGenerationParallelism()
        => IsChunkGenerationParallelEnabled ? Math.Max(1, Environment.ProcessorCount) : 1;

    //IsOptimized checks whether chunk generation parallelism is enabled
    public static bool IsOptimized => IsChunkGenerationParallelEnabled;

    //GetStats returns the WorldGen optimization stats for diagnostics
    public static WorldGenOptimizationStats GetStats() => new(
        ChunkGenerationParallel: IsChunkGenerationParallelEnabled,
        GenerationParallelism: GetGenerationParallelism(),
        IsOptimized: IsOptimized);
}

//WorldGen optimization stats snapshot
public readonly record struct WorldGenOptimizationStats(
    bool ChunkGenerationParallel,
    int GenerationParallelism,
    bool IsOptimized);
