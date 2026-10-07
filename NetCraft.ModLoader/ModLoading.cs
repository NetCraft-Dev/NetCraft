using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;

namespace NetCraft.ModLoader;

//ModManager loading flow: static scan, dependency resolution, topological sort, prepare, initialize
public sealed partial class ModManager
{
    //LoadAllModsAsync: scans and loads all mods in the mods directory
    //The scan phase loads no assemblies; loading begins only at the initialization phase
    public async Task<LoadResult> LoadAllModsAsync(ModEnvironment environment)
    {
        if (string.IsNullOrEmpty(_modsFolderPath))
            throw new InvalidOperationException("Init was not called to specify the mods directory");

        var result = new LoadResult();

        UpdateProgress("Scanning", 0, 0, string.Empty);
        var scan = ScanMods(environment);
        result.Errors.AddRange(scan.Errors);
        result.Skipped.AddRange(scan.Skipped);
        foreach (var mod in scan.Mods)
            _mods[mod.Name] = mod;

        if (result.Errors.Count > 0)
        {
            result.Success = false;
            return result;
        }

        UpdateProgress("ResolvingDeps", 0, _mods.Count, string.Empty);
        ValidateDependencyVersions(result);
        BuildDependencyGraph();

        var sorted = TopologicalSort();
        if (sorted is null)
        {
            result.Errors.Add("mod dependencies contain a cycle");
            result.Success = false;
            return result;
        }

        UpdateProgress("Initializing", 0, sorted.Count, string.Empty);
        _loadOrder.Clear();
        _loadOrder.AddRange(sorted.Select(m => m.Name));
        await InitializeModsAsync(sorted, result);

        result.Success = result.Errors.Count == 0;
        UpdateProgress("Done", sorted.Count, sorted.Count, result.Success ? "OK" : "PartialFail");
        return result;
    }

    //ScanMods: statically reads mod declarations and picks the mods to load on the current side
    //Reads only metadata tables and embedded resources without loading assemblies, so mods with a mismatched side are not pulled into the process
    private ScanResult ScanMods(ModEnvironment environment)
    {
        var result = new ScanResult();
        foreach (var scanned in ModScanner.ScanAll(_modsFolderPath))
        {
            if (!scanned.Manifest.Environment.Matches(environment))
            {
                result.Skipped.Add(scanned.Manifest.Id);
                continue;
            }

            if (scanned.Manifest.Entry.Length == 0)
            {
                result.Errors.Add($"mod {scanned.Manifest.Id} does not declare an entry class");
                continue;
            }

            result.Mods.Add(new InternalModInfo
            {
                Name = scanned.Manifest.Id,
                AssemblyPath = scanned.AssemblyPath,
                AssemblyName = scanned.AssemblyName,
                EmbeddedResources = scanned.EmbeddedResources.ToList(),
                ReferencedAssemblies = scanned.ReferencedAssemblies.ToList(),
                AnnotatedHooks = scanned.AnnotatedHooks.ToList(),
                Manifest = scanned.Manifest,
                Status = ModStatus.Scanned,
            });
        }
        return result;
    }

    //ValidateDependencyVersions: checks the dependency versions declared in the manifest
    //Bootstrap already filtered once with the same rules; this runs again for hosts that use ModManager on its own
    //A version mismatch only skips the declaring mod, the rest load as usual
    private void ValidateDependencyVersions(LoadResult result)
    {
        var manifests = new List<ModManifest>();
        foreach (var mod in _mods.Values)
        {
            if (mod.Manifest is not null)
                manifests.Add(mod.Manifest);
        }

        var skipped = new HashSet<string>(StringComparer.Ordinal);
        var errors = new List<string>();
        ModDependencies.Filter(manifests, skipped, errors);
        if (skipped.Count == 0)
            return;

        foreach (var name in skipped)
        {
            if (_mods.TryGetValue(name, out var mod))
                mod.Status = ModStatus.Skipped;
            result.Skipped.Add(name);
        }
        result.Errors.AddRange(errors);
    }

    //BuildDependencyGraph: builds the dependency graph from assembly references
    //Dependencies between mods form AssemblyRef naturally from compile-time references and need no extra declaration
    private void BuildDependencyGraph()
    {
        var byAssembly = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in _mods.Values)
        {
            if (mod.AssemblyName.Length > 0)
                byAssembly[mod.AssemblyName] = mod.Name;
        }

        foreach (var mod in _mods.Values)
        {
            mod.Dependencies = mod.ReferencedAssemblies
                .Where(byAssembly.ContainsKey)
                .Select(name => byAssembly[name])
                .Where(name => name != mod.Name)
                .Distinct()
                .ToList();
            _dependencyGraph[mod.Name] = mod.Dependencies;
        }
    }

    //TopologicalSort: orders dependencies first, returns null if there is a cycle
    private List<InternalModInfo>? TopologicalSort()
    {
        var sorted = new List<InternalModInfo>();
        var visiting = new HashSet<string>();
        var done = new HashSet<string>();

        bool Visit(string name)
        {
            if (done.Contains(name)) return true;
            if (!visiting.Add(name)) return false;
            if (_dependencyGraph.TryGetValue(name, out var deps))
            {
                foreach (var dep in deps)
                {
                    if (_mods.ContainsKey(dep) && !Visit(dep))
                        return false;
                }
            }
            visiting.Remove(name);
            done.Add(name);
            if (_mods.TryGetValue(name, out var mod))
                sorted.Add(mod);
            return true;
        }

        foreach (var name in _mods.Keys)
        {
            if (!done.Contains(name) && !Visit(name))
                return null;
        }

        return sorted;
    }

    //InitializeModsAsync: runs the load control and initializes each mod in turn
    private async Task InitializeModsAsync(List<InternalModInfo> sorted, LoadResult result)
    {
        var pending = sorted.Where(m => m.Status == ModStatus.Scanned).ToList();
        var completed = 0;

        foreach (var mod in pending)
        {
            UpdateProgress("Initializing", completed, pending.Count, mod.Name);

            var context = new ModLoadContext
            {
                Name = mod.Name,
                AssemblyPath = mod.AssemblyPath,
                Dependencies = mod.Dependencies,
                Completed = completed,
                Total = pending.Count,
            };

            var action = ModLoadAction.Continue;
            if (OnBeforeModLoad is not null)
            {
                try
                {
                    action = await OnBeforeModLoad(context);
                }
                catch (Exception ex)
                {
                    OnError?.Invoke($"pre-load callback of mod {mod.Name} threw", ex);
                    action = ModLoadAction.Skip;
                }
            }

            if (action == ModLoadAction.Skip)
            {
                mod.Status = ModStatus.Skipped;
                result.Skipped.Add(mod.Name);
                OnModLoadComplete?.Invoke(context, ModStatus.Skipped);
                completed++;
                continue;
            }

            if (action == ModLoadAction.Interrupt)
            {
                await HandleInterruptAsync(mod, context, result);
                completed++;
                continue;
            }

            await InitializeModAsync(mod, context, result);
            completed++;
        }
    }

    //HandleInterruptAsync: the host takes over loading; the result decides whether to mark failed, mark loaded, stop midway, or fall back to the default flow
    private async Task HandleInterruptAsync(InternalModInfo mod, ModLoadContext context, LoadResult result)
    {
        InterruptResult? interruptResult = null;
        if (OnModInterrupted is not null)
        {
            try
            {
                interruptResult = await OnModInterrupted(context);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"intercept callback of mod {mod.Name} threw", ex);
                interruptResult = new InterruptResult { Error = ex.Message };
            }
        }

        if (interruptResult is not null && interruptResult.Error.Length > 0)
        {
            mod.Status = ModStatus.Error;
            result.Errors.Add($"mod {mod.Name} takeover failed {interruptResult.Error}");
            ModFailed?.Invoke(mod.ToPublic(), new InvalidOperationException(interruptResult.Error));
            OnModLoadComplete?.Invoke(context, ModStatus.Error);
            return;
        }

        if (interruptResult is not null && interruptResult.MarkAsLoaded)
        {
            mod.Status = ModStatus.Running;
            result.Loaded.Add(mod.Name);
            ModLoaded?.Invoke(mod.ToPublic());
            OnModLoadComplete?.Invoke(context, ModStatus.Running);
            return;
        }

        if (interruptResult is not null && !interruptResult.ContinueOriginalLogic)
        {
            mod.Status = ModStatus.Interrupted;
            result.Interrupted.Add(mod.Name);
            OnModLoadComplete?.Invoke(context, ModStatus.Interrupted);
            return;
        }

        await InitializeModAsync(mod, context, result);
    }

    //InitializeModAsync: injects services and calls Init
    private async Task InitializeModAsync(InternalModInfo mod, ModLoadContext context, LoadResult result)
    {
        await mod.InitLock.WaitAsync();
        try
        {
            if (mod.Status != ModStatus.Scanned)
                return;

            if (!PrepareMod(mod, result))
            {
                mod.Status = ModStatus.Error;
                OnModLoadComplete?.Invoke(context, ModStatus.Error);
                return;
            }

            mod.Status = ModStatus.Initializing;
            InjectServices(mod.Instance);
            var startedAt = Stopwatch.GetTimestamp();
            if (mod.InitMethod!.Invoke(mod.Instance, null) is Task task)
                await task;
            mod.LoadMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

            mod.Status = ModStatus.Running;
            result.Loaded.Add(mod.Name);
            ModLoaded?.Invoke(mod.ToPublic());
            OnModLoadComplete?.Invoke(context, ModStatus.Running);
        }
        catch (Exception ex)
        {
            mod.Status = ModStatus.Error;
            result.Errors.Add($"mod {mod.Name} initialization failed {ex.Message}");
            OnError?.Invoke($"mod {mod.Name} initialization failed", ex);
            ModFailed?.Invoke(mod.ToPublic(), ex);
            OnModLoadComplete?.Invoke(context, ModStatus.Error);
        }
        finally
        {
            mod.InitLock.Release();
        }
    }

    //PrepareMod: loads the assembly and resolves the entry class
    //Reuses the assembly already in Default; loading a second copy under the same name breaks type equality
    private static bool PrepareMod(InternalModInfo mod, LoadResult result)
    {
        if (mod.EntryType is not null)
            return true;

        try
        {
            var assembly = ModAssemblies.Load(mod.AssemblyName, mod.AssemblyPath);

            var entryName = mod.Manifest?.Entry ?? string.Empty;
            var entryType = assembly.GetType(entryName, throwOnError: false);
            if (entryType is null)
            {
                result.Errors.Add($"mod {mod.Name} entry class {entryName} not found");
                return false;
            }

            var init = entryType.GetMethod("Init", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (init is null || init.ReturnType != typeof(Task))
            {
                result.Errors.Add($"mod {mod.Name} entry class lacks public Task Init()");
                return false;
            }

            mod.EntryType = entryType;
            mod.Instance = Activator.CreateInstance(entryType);
            mod.InitMethod = init;
            mod.ExitMethod = entryType.GetMethod("Exit", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            return true;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"mod {mod.Name} load failed {ex.Message}");
            return false;
        }
    }
}
