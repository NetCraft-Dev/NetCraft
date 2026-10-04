using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;

namespace NetCraft.ModLoader;

//ModManager 加载流程 静态扫描 依赖解析 拓扑排序 准备 初始化
public sealed partial class ModManager
{
    //LoadAllModsAsync 扫描并加载模组目录下的全部模组
    //扫描阶段不加载任何程序集 加载从初始化阶段才开始
    public async Task<LoadResult> LoadAllModsAsync(ModEnvironment environment)
    {
        if (string.IsNullOrEmpty(_modsFolderPath))
            throw new InvalidOperationException("未调用 Init 指定模组目录");

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
            result.Errors.Add("模组依赖存在环");
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

    //ScanMods 静态读模组声明挑出当前端要加载的模组
    //全程只读元数据表与内嵌资源 不加载程序集 这样运行端不匹配的模组不会被拉进进程
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
                result.Errors.Add($"模组 {scanned.Manifest.Id} 没有声明入口类");
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

    //ValidateDependencyVersions 校验清单里声明的依赖版本
    //引导阶段已经用同一份规则剔过一轮 这里再走一遍是为了单独使用 ModManager 的宿主
    //版本不符只跳过声明方 其余模组照常加载
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

    //BuildDependencyGraph 按程序集引用建依赖图
    //模组间的依赖由编译期引用自然形成 AssemblyRef 不需要额外声明
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

    //TopologicalSort 依赖优先排序 存在环时返回 null
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

    //InitializeModsAsync 依次走加载控制并初始化各模组
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
                    OnError?.Invoke($"模组 {mod.Name} 的加载前置回调异常", ex);
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

    //HandleInterruptAsync 宿主接管加载 按结果决定标记失败 标记已加载 停在中途 还是回到默认流程
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
                OnError?.Invoke($"模组 {mod.Name} 的接管回调异常", ex);
                interruptResult = new InterruptResult { Error = ex.Message };
            }
        }

        if (interruptResult is not null && interruptResult.Error.Length > 0)
        {
            mod.Status = ModStatus.Error;
            result.Errors.Add($"模组 {mod.Name} 接管失败 {interruptResult.Error}");
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

    //InitializeModAsync 注入服务并调 Init
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
            result.Errors.Add($"模组 {mod.Name} 初始化失败 {ex.Message}");
            OnError?.Invoke($"模组 {mod.Name} 初始化失败", ex);
            ModFailed?.Invoke(mod.ToPublic(), ex);
            OnModLoadComplete?.Invoke(context, ModStatus.Error);
        }
        finally
        {
            mod.InitLock.Release();
        }
    }

    //PrepareMod 加载程序集并解析入口类
    //程序集已在 Default 里时直接复用 同名加载出两份会让类型判等失败
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
                result.Errors.Add($"模组 {mod.Name} 找不到入口类 {entryName}");
                return false;
            }

            var init = entryType.GetMethod("Init", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (init is null || init.ReturnType != typeof(Task))
            {
                result.Errors.Add($"模组 {mod.Name} 的入口类缺少 public Task Init()");
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
            result.Errors.Add($"模组 {mod.Name} 加载失败 {ex.Message}");
            return false;
        }
    }
}
