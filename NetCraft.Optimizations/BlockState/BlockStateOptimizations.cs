using NetCraft.Config;
using NetCraft.Registry.State;

namespace NetCraft.Optimizations.BlockState;

//BlockState optimization module, covers core optimization point 2.5
//The actual optimization implements BlockState as a readonly struct in NetCraft.Registry/State/BlockState.cs
//BlockStateRegistry stores all BlockStateData centrally so no per-instance array is held
//This module is the optimization facade, exposing toggle queries and the stats API
public static class BlockStateOptimizations
{
    public const string ModuleName = "BlockState Optimization";
    public const string TargetSubsystem = "NetCraft.Registry (Block/BlockState)";

    //Optimization point 2.5, uses the FerriteCore FastMap equivalent approach
    //BlockState struct conversion and int encoding are implemented in NetCraft.Registry/State/BlockState.cs
    public static bool IsIntEncodedEnabled => OptimizationFlags.BlockStateIntEncoded;

    //Compact array storage for BlockState properties is implemented in BlockStateRegistry
    //Replaces the vanilla Map<Property, Comparable> and saves the per-instance Map overhead
    public static bool IsPropertyCompactArrayEnabled => OptimizationFlags.BlockStatePropertyCompactArray;

    //Precomputed neighbors table for BlockStateCache is implemented in BlockStateRegistry.InitializeNeighbors
    //setValue does a direct table lookup instead of iterating the possible states
    public static bool IsCachePrecomputedEnabled => OptimizationFlags.BlockStateCachePrecomputed;

    //IsOptimized checks whether all three toggles are on to decide if BlockState optimization is enabled
    public static bool IsOptimized =>
        IsIntEncodedEnabled && IsPropertyCompactArrayEnabled && IsCachePrecomputedEnabled;

    //GetStats returns the BlockState optimization stats for diagnostics
    //totalStates is the number of registered BlockStates, maps to _all.Count
    //totalProperties is the sum over all state property keys, aligned with the memory footprint estimate
    public static BlockStateOptimizationStats GetStats()
    {
        var totalStates = BlockStateRegistry.Count;
        long totalProperties = 0;
        for (var i = 0; i < totalStates; i++)
        {
            totalProperties += BlockStateRegistry.GetProperties(i).Count;
        }
        return new BlockStateOptimizationStats(
            TotalStates: totalStates,
            TotalProperties: totalProperties,
            IsOptimized: IsOptimized);
    }
}

//BlockState optimization stats snapshot
public readonly record struct BlockStateOptimizationStats(
    int TotalStates,
    long TotalProperties,
    bool IsOptimized);
