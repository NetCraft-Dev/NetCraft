using NetCraft.Config;

namespace NetCraft.Optimizations.PalettedContainer;

//PalettedContainer optimization module, covers core optimization point 2.10
//The actual optimization is integrated in NetCraft.Storage/BitStorage/SimpleBitStorage.cs
//SimpleBitStorage already uses compact long[] bit storage + a magic-number fast division table, a Span equivalent
//Borrows from ModernFix CompactBitStorage + C2ME vectorized_algorithms, double validated
public static class PalettedContainerOptimizations
{
    public const string ModuleName = "PalettedContainer Optimization";
    public const string TargetSubsystem = "NetCraft.Storage (PalettedContainer / BitStorage)";

    //Optimization point 2.10, PalettedContainer uses SIMD bulk read/write
    //When the toggle is on, SimpleBitStorage already uses a compact long[] layout that is SIMD friendly
    //The JIT auto-vectorizes the long unpacking loop on the GetAll/Unpack path
    public static bool IsPalettedContainerSimdEnabled => OptimizationFlags.PalettedContainerSimd;

    //Optimization point 2.11, ClassInstanceMultiMap uses FrozenDictionary + ImmutableArray
    //When the toggle is on, entity class lookups go through the FrozenDictionary equivalent implementation
    public static bool IsClassInstanceMultiMapFrozenEnabled => OptimizationFlags.ClassInstanceMultiMapFrozen;

    //Optimization point 2.10, BitStorage implemented with Span<ulong> + BitOperations
    //When the toggle is on, SimpleBitStorage already uses a compact long[] layout that maps to a Span<ulong> view
    public static bool IsBitStorageSpanBasedEnabled => OptimizationFlags.BitStorageSpanBased;

    //IsOptimized checks whether all three toggles are on to decide if PalettedContainer optimization is enabled
    public static bool IsOptimized =>
        IsPalettedContainerSimdEnabled && IsClassInstanceMultiMapFrozenEnabled && IsBitStorageSpanBasedEnabled;

    //GetStats returns the PalettedContainer optimization stats for diagnostics
    public static PalettedContainerOptimizationStats GetStats() => new(
        PalettedContainerSimd: IsPalettedContainerSimdEnabled,
        ClassInstanceMultiMapFrozen: IsClassInstanceMultiMapFrozenEnabled,
        BitStorageSpanBased: IsBitStorageSpanBasedEnabled,
        IsOptimized: IsOptimized);
}

//PalettedContainer optimization stats snapshot
public readonly record struct PalettedContainerOptimizationStats(
    bool PalettedContainerSimd,
    bool ClassInstanceMultiMapFrozen,
    bool BitStorageSpanBased,
    bool IsOptimized);
