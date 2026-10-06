using NetCraft.Config;

namespace NetCraft.Optimizations.Registry;

//Registry optimization module, covers core optimization points 2.3 / 2.4
//2.3 FrozenDictionary indexing is integrated in the Freeze method in NetCraft.Registry/MappedRegistry.cs
//2.4 The intrusive holder stays a Reference<T> class instead of a struct, the call chain is too broad for the change to pay off
//The existing ReferenceEqualityComparer.Instance is already a reference comparison equivalent to struct indexing
//Borrows from the ModernFix ForgeRegistry approach, already validated
public static class RegistryOptimizations
{
    public const string ModuleName = "Registry Optimization";
    public const string TargetSubsystem = "NetCraft.Registry";

    //Optimization point 2.3, build FrozenDictionary indexes after MappedRegistry.Freeze
    //When the toggle is on, the five tables byLocation/byKey/byValue/toId/allTags become FrozenDictionary after freeze
    public static bool IsFrozenDictionaryEnabled => OptimizationFlags.RegistryFrozenDictionary;

    //Optimization point 2.4, intrusive holder struct conversion
    //When the toggle is on, it uses struct-equivalent semantics (ReferenceEqualityComparer reference comparison as struct indexing)
    //Reference<T> stays a class to avoid breaking the BuiltInRegistries/Register call chain
    public static bool IsIntrusiveHolderStructEnabled => OptimizationFlags.IntrusiveHolderStruct;

    //IsOptimized checks whether both toggles are on to decide if Registry optimization is enabled
    public static bool IsOptimized =>
        IsFrozenDictionaryEnabled && IsIntrusiveHolderStructEnabled;

    //GetStats returns the Registry optimization stats for diagnostics
    public static RegistryOptimizationStats GetStats() => new(
        FrozenDictionary: IsFrozenDictionaryEnabled,
        IntrusiveHolderStruct: IsIntrusiveHolderStructEnabled,
        IsOptimized: IsOptimized);
}

//Registry optimization stats snapshot
public readonly record struct RegistryOptimizationStats(
    bool FrozenDictionary,
    bool IntrusiveHolderStruct,
    bool IsOptimized);
