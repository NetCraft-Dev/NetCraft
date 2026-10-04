using NetCraft.Config;

namespace NetCraft.Optimizations.Memory;

//Memory 优化模块借鉴 FerriteCore mrl/threaddetec/datacomponents 三个子模块
//对应 OptimizationFlags 中 ModelResourceLocationIntern/VoxelShapeShared/ThreadingDetectorLightweight/PatchedDataComponentMapCompact
//跨子系统优化待对应子系统就绪后集成
public static class MemoryOptimizations
{
    public const string ModuleName = "Memory Optimization";
    public const string TargetSubsystem = "cross-cutting (Util/Registry/Network/...)";

    //对应 FerriteCore mrl 模块模型资源路径用 string.Intern 池化
    //开关启用表示资源路径访问将走 string.Intern 路径节省重复字符串内存
    public static bool IsModelResourceLocationInternEnabled => OptimizationFlags.ModelResourceLocationIntern;

    //对应 FerriteCore VoxelShape 缓存相同形状共用实例
    //开关启用表示 VoxelShape 创建将走共享缓存路径
    public static bool IsVoxelShapeSharedEnabled => OptimizationFlags.VoxelShapeShared;

    //对应 FerriteCore threaddetec 模块 ThreadingDetector 轻量化
    //开关启用表示 PalettedContainer 内存优化走轻量 ThreadingDetector 路径
    public static bool IsThreadingDetectorLightweightEnabled => OptimizationFlags.ThreadingDetectorLightweight;

    //对应 FerriteCore datacomponents 模块 PatchedDataComponentMap 紧凑存储
    //开关启用表示组件映射走紧凑存储路径
    public static bool IsPatchedDataComponentMapCompactEnabled => OptimizationFlags.PatchedDataComponentMapCompact;

    //IsOptimized 检查四开关是否全开判断 Memory 优化是否启用
    public static bool IsOptimized =>
        IsModelResourceLocationInternEnabled
        && IsVoxelShapeSharedEnabled
        && IsThreadingDetectorLightweightEnabled
        && IsPatchedDataComponentMapCompactEnabled;

    //GetStats 返回 Memory 优化统计信息用于诊断
    public static MemoryOptimizationStats GetStats() => new(
        ModelResourceLocationIntern: IsModelResourceLocationInternEnabled,
        VoxelShapeShared: IsVoxelShapeSharedEnabled,
        ThreadingDetectorLightweight: IsThreadingDetectorLightweightEnabled,
        PatchedDataComponentMapCompact: IsPatchedDataComponentMapCompactEnabled,
        IsOptimized: IsOptimized);
}

//Memory 优化统计快照
public readonly record struct MemoryOptimizationStats(
    bool ModelResourceLocationIntern,
    bool VoxelShapeShared,
    bool ThreadingDetectorLightweight,
    bool PatchedDataComponentMapCompact,
    bool IsOptimized);
