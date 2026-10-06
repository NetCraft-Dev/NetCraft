using NetCraft.Config;

namespace NetCraft.Optimizations.Memory;

//Memory optimization module, borrows from the three FerriteCore submodules mrl/threaddetec/datacomponents
//Maps to OptimizationFlags ModelResourceLocationIntern/VoxelShapeShared/ThreadingDetectorLightweight/PatchedDataComponentMapCompact
//Cross-subsystem optimizations land once the corresponding subsystems are ready
public static class MemoryOptimizations
{
    public const string ModuleName = "Memory Optimization";
    public const string TargetSubsystem = "cross-cutting (Util/Registry/Network/...)";

    //The FerriteCore mrl module, model resource paths are interned with string.Intern
    //When the toggle is on, resource path access goes through the string.Intern path and saves duplicate string memory
    public static bool IsModelResourceLocationInternEnabled => OptimizationFlags.ModelResourceLocationIntern;

    //The FerriteCore approach, the VoxelShape cache shares instances for identical shapes
    //When the toggle is on, VoxelShape creation goes through the shared cache path
    public static bool IsVoxelShapeSharedEnabled => OptimizationFlags.VoxelShapeShared;

    //The FerriteCore threaddetec module, a lightweight ThreadingDetector
    //When the toggle is on, the PalettedContainer memory optimization goes through the lightweight ThreadingDetector path
    public static bool IsThreadingDetectorLightweightEnabled => OptimizationFlags.ThreadingDetectorLightweight;

    //The FerriteCore datacomponents module, compact storage for PatchedDataComponentMap
    //When the toggle is on, the component map goes through the compact storage path
    public static bool IsPatchedDataComponentMapCompactEnabled => OptimizationFlags.PatchedDataComponentMapCompact;

    //IsOptimized checks whether all four toggles are on to decide if Memory optimization is enabled
    public static bool IsOptimized =>
        IsModelResourceLocationInternEnabled
        && IsVoxelShapeSharedEnabled
        && IsThreadingDetectorLightweightEnabled
        && IsPatchedDataComponentMapCompactEnabled;

    //GetStats returns the Memory optimization stats for diagnostics
    public static MemoryOptimizationStats GetStats() => new(
        ModelResourceLocationIntern: IsModelResourceLocationInternEnabled,
        VoxelShapeShared: IsVoxelShapeSharedEnabled,
        ThreadingDetectorLightweight: IsThreadingDetectorLightweightEnabled,
        PatchedDataComponentMapCompact: IsPatchedDataComponentMapCompactEnabled,
        IsOptimized: IsOptimized);
}

//Memory optimization stats snapshot
public readonly record struct MemoryOptimizationStats(
    bool ModelResourceLocationIntern,
    bool VoxelShapeShared,
    bool ThreadingDetectorLightweight,
    bool PatchedDataComponentMapCompact,
    bool IsOptimized);
