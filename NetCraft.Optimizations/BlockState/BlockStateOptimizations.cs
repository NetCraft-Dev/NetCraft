using NetCraft.Config;
using NetCraft.Registry.State;

namespace NetCraft.Optimizations.BlockState;

//BlockState 优化模块对应核心优化点 2.5
//实际优化在 NetCraft.Registry/State/BlockState.cs 实现 readonly struct
//BlockStateRegistry 集中存储所有 BlockStateData 避免每实例持数组
//此模块作为优化门面提供开关查询与统计API
public static class BlockStateOptimizations
{
    public const string ModuleName = "BlockState Optimization";
    public const string TargetSubsystem = "NetCraft.Registry (Block/BlockState)";

    //对应优化点 2.5 采用 FerriteCore FastMap 等价方案
    //BlockState struct 化 int 编码已实现于 NetCraft.Registry/State/BlockState.cs
    public static bool IsIntEncodedEnabled => OptimizationFlags.BlockStateIntEncoded;

    //BlockState 属性紧凑数组存储已实现于 BlockStateRegistry
    //替代原版 Map<Property, Comparable> 节省每实例 Map 开销
    public static bool IsPropertyCompactArrayEnabled => OptimizationFlags.BlockStatePropertyCompactArray;

    //BlockStateCache 预计算 neighbors 表已实现于 BlockStateRegistry.InitializeNeighbors
    //setValue 直接查表避免遍历 possible states
    public static bool IsCachePrecomputedEnabled => OptimizationFlags.BlockStateCachePrecomputed;

    //IsOptimized 检查三个开关是否全开判断 BlockState 优化是否启用
    public static bool IsOptimized =>
        IsIntEncodedEnabled && IsPropertyCompactArrayEnabled && IsCachePrecomputedEnabled;

    //GetStats 返回 BlockState 优化统计信息用于诊断
    //totalStates 为已注册 BlockState 总数对应 _all.Count
    //totalProperties 为所有状态属性键总和对齐内存占用估算
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

//BlockState 优化统计快照
public readonly record struct BlockStateOptimizationStats(
    int TotalStates,
    long TotalProperties,
    bool IsOptimized);
