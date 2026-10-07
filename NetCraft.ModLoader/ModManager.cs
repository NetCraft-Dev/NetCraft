using System.Collections.Concurrent;

namespace NetCraft.ModLoader;

//ModManager: the mod manager, maps to the loading duties of vanilla Fabric Loader
//Completed once during loading and read-only at runtime, no dynamic loading, unloading, or unmounting is offered
public sealed partial class ModManager
{
    private string _modsFolderPath = string.Empty;
    private readonly Dictionary<string, InternalModInfo> _mods = new();
    private readonly Dictionary<string, List<string>> _dependencyGraph = new();
    private readonly List<string> _loadOrder = new();
    private readonly ConcurrentDictionary<Type, object> _services = new();
    private readonly ProgressInfo _progress = new();

    //ModLoaded: mod initialized successfully
    public event Action<ModInfo>? ModLoaded;
    //ModFailed: mod failed to load or initialize
    public event Action<ModInfo, Exception>? ModFailed;
    //ProgressUpdated: load progress changed
    public event Action<ProgressInfo>? ProgressUpdated;
    //OnError: exception within the flow
    public event Action<string, Exception>? OnError;
    //OnBeforeModLoad: pre-load hook; the host can return skip or take over
    public event Func<ModLoadContext, Task<ModLoadAction>>? OnBeforeModLoad;
    //OnModInterrupted: callback when the host takes over loading
    public event Func<ModLoadContext, Task<InterruptResult>>? OnModInterrupted;
    //OnModLoadComplete: a single mod's flow finished
    public event Action<ModLoadContext, ModStatus>? OnModLoadComplete;

    //Progress: the current progress snapshot
    public ProgressInfo Progress => _progress;

    //Init: specifies the mods directory, created automatically if missing
    public void Init(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            throw new ArgumentException("mods directory cannot be empty", nameof(folderPath));
        _modsFolderPath = Path.GetFullPath(folderPath);
        if (!Directory.Exists(_modsFolderPath))
            Directory.CreateDirectory(_modsFolderPath);
    }

    //RegisterService: registers a service instance for mod injection
    public void RegisterService<T>(T service) where T : class => _services[typeof(T)] = service;

    //GetService: gets a registered service
    public T? GetService<T>() where T : class
        => _services.TryGetValue(typeof(T), out var service) ? (T)service : null;

    //GetAllMods: information on all mods
    public IReadOnlyList<ModInfo> GetAllMods() => _mods.Values.Select(m => m.ToPublic()).ToList();

    //GetAllModNames: names of all mods
    public IReadOnlyList<string> GetAllModNames() => _mods.Keys.ToList();

    //GetLoadedMods: information on mods that finished loading
    public IReadOnlyList<ModInfo> GetLoadedMods()
        => _mods.Values.Where(m => m.Status == ModStatus.Running).Select(m => m.ToPublic()).ToList();

    //GetLoadedModNames: names of mods that finished loading
    public IReadOnlyList<string> GetLoadedModNames()
        => _mods.Values.Where(m => m.Status == ModStatus.Running).Select(m => m.Name).ToList();

    //GetStatus: queries a mod's status, NotFound when not tracked
    public ModStatus GetStatus(string name) => _mods.TryGetValue(name, out var mod) ? mod.Status : ModStatus.NotFound;

    //IsLoaded: whether a mod finished loading
    public bool IsLoaded(string name) => GetStatus(name) == ModStatus.Running;

    //GetModInfo: queries mod details
    public ModInfo? GetModInfo(string name) => _mods.TryGetValue(name, out var mod) ? mod.ToPublic() : null;

    //GetDependencies: names of the mods it depends on
    public IReadOnlyList<string> GetDependencies(string name)
        => _dependencyGraph.TryGetValue(name, out var deps) ? deps : Array.Empty<string>();

    //GetModsDependingOn: reverse lookup of which mods depend on it
    public IReadOnlyList<string> GetModsDependingOn(string name)
        => _dependencyGraph.Where(kv => kv.Value.Contains(name)).Select(kv => kv.Key).ToList();

    //GetLoadOrder: the actual initialization order, i.e. the topological sort result
    public IReadOnlyList<string> GetLoadOrder() => _loadOrder;

    //ReadResource: reads any embedded resource bytes of a mod by name
    //The UI reads icons through here; returns null if not found or unreadable
    public byte[]? ReadResource(string name, string resourceName)
    {
        if (string.IsNullOrEmpty(resourceName) || !_mods.TryGetValue(name, out var mod))
            return null;

        try
        {
            return ModScanner.ReadEmbeddedResource(mod.AssemblyPath, resourceName);
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"failed to read resource {resourceName} of mod {name}", ex);
            return null;
        }
    }

    //ReadIcon: gets the mod icon bytes
    //First follows the manifest icon; falls back to the embedded resource named icon.png when the manifest omits it or the target is missing
    //Returns null when neither exists, leaving the UI to choose its own placeholder image
    public byte[]? ReadIcon(string name)
    {
        if (!_mods.TryGetValue(name, out var mod))
            return null;

        var declared = mod.Manifest?.Icon ?? string.Empty;
        if (declared.Length > 0)
        {
            var bytes = ReadResource(name, declared);
            if (bytes is not null)
                return bytes;
        }

        var fallback = mod.EmbeddedResources.FirstOrDefault(IsConventionalIcon);
        return fallback is null ? null : ReadResource(name, fallback);
    }

    //IsConventionalIcon: whether an embedded resource name matches the conventional icon name
    //The default name may carry a project namespace prefix, so it is matched by suffix
    private static bool IsConventionalIcon(string resourceName)
        => resourceName.Equals("icon.png", StringComparison.OrdinalIgnoreCase)
            || resourceName.EndsWith(".icon.png", StringComparison.OrdinalIgnoreCase);

    //ShutdownAsync: calls each mod's Exit before the process exits, letting mods wrap up
    //No unloading; assemblies and registered content persist until the process ends
    public async Task ShutdownAsync()
    {
        foreach (var mod in _mods.Values)
        {
            if (mod.Status != ModStatus.Running || mod.ExitMethod is null || mod.Instance is null)
                continue;
            try
            {
                if (mod.ExitMethod.Invoke(mod.Instance, null) is Task task)
                    await task;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"mod {mod.Name} failed to exit", ex);
            }
        }
    }

    //UpdateProgress: refreshes progress and broadcasts it
    private void UpdateProgress(string status, int completed, int total, string currentMod)
    {
        _progress.Status = status;
        _progress.Completed = completed;
        _progress.Total = total;
        _progress.CurrentMod = currentMod;
        _progress.Percentage = total == 0 ? 0 : (int)((double)completed / total * 100);
        ProgressUpdated?.Invoke(_progress);
    }
}
