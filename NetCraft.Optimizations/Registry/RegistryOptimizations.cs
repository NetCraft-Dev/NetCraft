using NetCraft.Config;

namespace NetCraft.Optimizations.Registry;

//Registry 优化模块对应核心优化点 2.3 / 2.4
//2.3 FrozenDictionary 索引集成于 NetCraft.Registry/MappedRegistry.cs 的 Freeze 方法
//2.4 intrusive holder struct 化保持 Reference<T> class 形态因调用链广泛改造代价高
//现有 ReferenceEqualityComparer.Instance 已是引用比较等价 struct 索引语义
//借鉴 ModernFix ForgeRegistry 优化方案已验证
public static class RegistryOptimizations
{
    public const string ModuleName = "Registry Optimization";
    public const string TargetSubsystem = "NetCraft.Registry";

    //对应优化点 2.3 MappedRegistry.Freeze 后构建 FrozenDictionary 索引
    //开关启用时 byLocation/byKey/byValue/toId/allTags 五张表 freeze 后转 FrozenDictionary
    public static bool IsFrozenDictionaryEnabled => OptimizationFlags.RegistryFrozenDictionary;

    //对应优化点 2.4 intrusive holder struct 化
    //开关启用表示采用 struct 等价语义（ReferenceEqualityComparer 引用比较等价 struct 索引）
    //Reference<T> 保持 class 形态避免破坏 BuiltInRegistries/Register 调用链
    public static bool IsIntrusiveHolderStructEnabled => OptimizationFlags.IntrusiveHolderStruct;

    //IsOptimized 检查两开关是否全开判断 Registry 优化是否启用
    public static bool IsOptimized =>
        IsFrozenDictionaryEnabled && IsIntrusiveHolderStructEnabled;

    //GetStats 返回 Registry 优化统计信息用于诊断
    public static RegistryOptimizationStats GetStats() => new(
        FrozenDictionary: IsFrozenDictionaryEnabled,
        IntrusiveHolderStruct: IsIntrusiveHolderStructEnabled,
        IsOptimized: IsOptimized);
}

//Registry 优化统计快照
public readonly record struct RegistryOptimizationStats(
    bool FrozenDictionary,
    bool IntrusiveHolderStruct,
    bool IsOptimized);
