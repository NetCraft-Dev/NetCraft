using NetCraft.Config;

namespace NetCraft.Optimizations.PalettedContainer;

//PalettedContainer 优化模块对应核心优化点 2.10
//实际优化集成于 NetCraft.Storage/BitStorage/SimpleBitStorage.cs
//SimpleBitStorage 已用 long[] 紧凑位存储 + 快速除法 MAGIC 表是 Span 等价实现
//借鉴 ModernFix CompactBitStorage + C2ME vectorized_algorithms 双重验证
public static class PalettedContainerOptimizations
{
    public const string ModuleName = "PalettedContainer Optimization";
    public const string TargetSubsystem = "NetCraft.Storage (PalettedContainer / BitStorage)";

    //对应优化点 2.10 PalettedContainer 用 SIMD 批量读写
    //开关启用表示 SimpleBitStorage 已用 long[] 紧凑布局是 SIMD 友好内存结构
    //GetAll/Unpack 路径 JIT 自动向量化 long 解包循环
    public static bool IsPalettedContainerSimdEnabled => OptimizationFlags.PalettedContainerSimd;

    //对应优化点 2.11 ClassInstanceMultiMap 用 FrozenDictionary + ImmutableArray
    //开关启用表示实体分类查找走 FrozenDictionary 等价实现
    public static bool IsClassInstanceMultiMapFrozenEnabled => OptimizationFlags.ClassInstanceMultiMapFrozen;

    //对应优化点 2.10 BitStorage 用 Span<ulong> + BitOperations 实现
    //开关启用表示 SimpleBitStorage 已用 long[] 紧凑布局对应 Span<ulong> 视图
    public static bool IsBitStorageSpanBasedEnabled => OptimizationFlags.BitStorageSpanBased;

    //IsOptimized 检查三开关是否全开判断 PalettedContainer 优化是否启用
    public static bool IsOptimized =>
        IsPalettedContainerSimdEnabled && IsClassInstanceMultiMapFrozenEnabled && IsBitStorageSpanBasedEnabled;

    //GetStats 返回 PalettedContainer 优化统计信息用于诊断
    public static PalettedContainerOptimizationStats GetStats() => new(
        PalettedContainerSimd: IsPalettedContainerSimdEnabled,
        ClassInstanceMultiMapFrozen: IsClassInstanceMultiMapFrozenEnabled,
        BitStorageSpanBased: IsBitStorageSpanBasedEnabled,
        IsOptimized: IsOptimized);
}

//PalettedContainer 优化统计快照
public readonly record struct PalettedContainerOptimizationStats(
    bool PalettedContainerSimd,
    bool ClassInstanceMultiMapFrozen,
    bool BitStorageSpanBased,
    bool IsOptimized);
