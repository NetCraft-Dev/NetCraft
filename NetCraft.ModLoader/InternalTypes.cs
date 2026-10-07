using System.Reflection;

namespace NetCraft.ModLoader;

//InternalModInfo: the full mod information held internally during the loading flow
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

    //AssemblyName: the mod's assembly name, used to map AssemblyRef back to the mod
    public string AssemblyName { get; set; } = string.Empty;

    //ReferencedAssemblies: all assembly names this assembly references
    public List<string> ReferencedAssemblies { get; set; } = new();

    //Manifest: the parse result of the embedded ncmod.json
    public ModManifest? Manifest { get; set; }

    //AnnotatedHooks: injection rules scanned from Inject annotations, same shape as manifest rules
    public List<ModHookRule> AnnotatedHooks { get; set; } = new();

    //EmbeddedResources: all embedded resource names in the assembly, the icon fallback looks here
    public List<string> EmbeddedResources { get; set; } = new();

    //LoadMilliseconds: initialization time in milliseconds, used by the UI to show slow-loading mods, null if initialization was not reached
    public double? LoadMilliseconds { get; set; }

    //InitLock: ensures a mod is initialized only once
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

    //MergedHooks: merges annotations and manifest into one list for the UI
    //Annotations come first, matching assembly-time priority; only the first rule per injection point is kept
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

//ScanResult: the output of the scan phase
internal sealed class ScanResult
{
    public List<InternalModInfo> Mods { get; } = new();
    public List<string> Errors { get; } = new();
    public List<string> Skipped { get; } = new();
}
