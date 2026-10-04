using System.Reflection;

namespace NetCraft.ModLoader;

//InternalModInfo 加载流程内部持有的模组完整信息
internal sealed class InternalModInfo
{
    public string Name { get; set; } = string.Empty;
    public Type? EntryType { get; set; }
    public object? Instance { get; set; }
    public string AssemblyPath { get; set; } = string.Empty;
    public MethodInfo? InitMethod { get; set; }
    public MethodInfo? ExitMethod { get; set; }
    public List<string> Dependencies { get; set; } = new();
    public ModStatus Status { get; set; }

    //AssemblyName 模组程序集名 用于把 AssemblyRef 映射回模组
    public string AssemblyName { get; set; } = string.Empty;

    //ReferencedAssemblies 该程序集引用的全部程序集名
    public List<string> ReferencedAssemblies { get; set; } = new();

    //Manifest 内嵌 ncmod.json 的解析结果
    public ModManifest? Manifest { get; set; }

    //AnnotatedHooks 从 Inject 注解扫出的注入规则 与清单规则同形
    public List<ModHookRule> AnnotatedHooks { get; set; } = new();

    //EmbeddedResources 程序集内嵌的全部资源名 图标兜底要在里面找
    public List<string> EmbeddedResources { get; set; } = new();

    //LoadMilliseconds 初始化耗时 毫秒 界面显示加载慢的模组靠它 未走到初始化时为 null
    public double? LoadMilliseconds { get; set; }

    //InitLock 保证同一模组只初始化一次
    public SemaphoreSlim InitLock { get; } = new(1, 1);

    public ModInfo ToPublic() => new()
    {
        Name = Name,
        DisplayName = Manifest?.EffectiveName ?? Name,
        Version = Manifest?.Version ?? string.Empty,
        Description = Manifest?.Description ?? string.Empty,
        Authors = Manifest?.Authors ?? (IReadOnlyList<string>)Array.Empty<string>(),
        Contributors = Manifest?.Contributors ?? (IReadOnlyList<string>)Array.Empty<string>(),
        License = Manifest?.License ?? string.Empty,
        Contact = Manifest?.Contact ?? new ModContact(),
        Icon = Manifest?.Icon ?? string.Empty,
        Environment = Manifest?.Environment ?? ModEnvironment.Both,
        Hooks = MergedHooks(),
        LoadMilliseconds = LoadMilliseconds,
        EntryType = EntryType,
        Instance = Instance,
        AssemblyPath = AssemblyPath,
        Status = Status,
        Dependencies = Dependencies,
    };

    //MergedHooks 注解与清单合成一份给界面看
    //注解排在前 与装配时的优先级一致 同一个注入点只留前面那条
    private IReadOnlyList<ModHookRule> MergedHooks()
    {
        var manifestHooks = Manifest?.Hooks ?? (IReadOnlyList<ModHookRule>)Array.Empty<ModHookRule>();
        var declared = new HashSet<string>(StringComparer.Ordinal);
        var merged = new List<ModHookRule>();
        foreach (var rule in AnnotatedHooks.Concat(manifestHooks))
        {
            if (declared.Add($"{rule.Target}|{rule.Method}|{rule.HookTypeName}"))
                merged.Add(rule);
        }
        return merged;
    }
}

//ScanResult 扫描阶段的产出
internal sealed class ScanResult
{
    public List<InternalModInfo> Mods { get; } = new();
    public List<string> Errors { get; } = new();
    public List<string> Skipped { get; } = new();
}
