using System.Runtime.Loader;
using NetCraft.Logging;

namespace NetCraft.ModLoader;

//ModBootstrap 模组引导入口
//必须挂在每个可执行入口的最开头 内核程序集是按需解析的 一旦进来改写就没机会了
public static class ModBootstrap
{
    //Run 静态扫描模组目录 装配注入规则 再按依赖顺序初始化各模组
    //modsFolder 为空时取程序根目录下的 mods
    public static ModBootstrapResult Run(ModEnvironment environment, string? modsFolder = null)
    {
        var folder = modsFolder ?? Path.Combine(AppPaths.BaseDirectory, "mods");
        var scanned = ModScanner.ScanAll(folder)
            .Where(m => m.Manifest.Environment.Matches(environment))
            .ToList();

        //版本不匹配的模组要在这里就剔掉 它们的注入规则同样不能进规则表
        scanned = ApplyDependencyVersions(scanned);

        //内嵌依赖的解析要早于装配 装配会解析替换类 那一刻它引用的依赖就得能取到
        ModLibs.Register(scanned);

        var kernelNames = CollectKernelAssemblies();
        var hooks = ModHooks.Build(scanned, kernelNames, environment);

        //ReJIT 只能在进程启动那一刻打开 有运行时注入规则就得带着原生层重来一次
        //重启失败不阻断 那批规则在提交时会被判为不可用并留下警告
        if (hooks.RuntimeTargets.Count > 0 && !ProfilerRelaunch.Attached)
            ProfilerRelaunch.Relaunch();

        if (hooks.TargetAssemblies.Count > 0 || hooks.RuntimeTargets.Count > 0)
        {
            //模组程序集也要过改写器 模组之间互相注入就发生在它被加载的那一刻
            ModAssemblies.Rewriter = hooks.Rewrite;
            EmbeddedAssemblyLoader.SetRewriter(hooks.Rewrite);
            //替换方先起来 被注入的那个轮到时才有得改写
            hooks.PreloadReplacers();
            PreloadTargets(hooks, kernelNames);
            //RuntimePatch 要等目标程序集都加载完才落 放在模组 Init 之前 时机确定
            hooks.ApplyRuntimePatches();
            //运行时注入同样要求目标已在进程里 顺序放在内核预载之后
            hooks.ApplyRuntimeInjects();
        }

        //装配与预载过程中攒下的问题统一在这里报
        foreach (var error in hooks.Errors)
            Log.Warning($"Mod injection rule error {error}");

        //注入点撞车不算错误 只是后来的那条不生效 不报出来就没人知道
        foreach (var warning in hooks.Warnings)
            Log.Warning($"Mod injection conflict {warning}");

        if (hooks.TargetAssemblies.Count == 0 && hooks.RuntimeTargets.Count == 0)
        {
            Log.Info($"Mod scan finished: {scanned.Count} found, no injection rules");
        }
        else
        {
            Log.Info($"Mod scan finished: {scanned.Count} found, injection targets {string.Join(",", hooks.TargetAssemblies)}" +
                     $" runtime {string.Join(",", hooks.RuntimeTargets)}");
        }

        //初始化必须排在预载之后 模组若在 Init 里碰到内核类型会触发解析
        //那时目标程序集已是改写版 反过来则会把未改写的那份拉进来
        var manager = new ModManager();
        manager.Init(folder);
        var load = manager.LoadAllModsAsync(environment).GetAwaiter().GetResult();

        foreach (var error in load.Errors)
            Log.Warning($"Mod init error {error}");

        Log.Info($"Mod init finished: {load.Loaded.Count} loaded, {load.Skipped.Count} skipped");

        //登记给宿主组件 界面这类不在加载流程里的代码从 ModHost 取数据
        ModHost.Bind(manager, folder);
        return new ModBootstrapResult { Hooks = hooks, Manager = manager, Load = load };
    }

    //ApplyDependencyVersions 按清单声明的依赖版本剔除不兼容的模组
    //必须排在规则装配之前 被剔除的模组若还留着规则 照样会改写内核与其它模组
    private static List<ScannedMod> ApplyDependencyVersions(List<ScannedMod> scanned)
    {
        var skipped = new HashSet<string>(StringComparer.Ordinal);
        var errors = new List<string>();
        ModDependencies.Filter(scanned.Select(m => m.Manifest).ToList(), skipped, errors);
        if (skipped.Count == 0)
            return scanned;

        foreach (var error in errors)
            Log.Warning($"Mod dependency unmet {error}");
        return scanned.Where(m => !skipped.Contains(m.Manifest.Id)).ToList();
    }

    //CollectKernelAssemblies 可注入的内核程序集名
    //内嵌子库来自主库内嵌资源 上层程序集 Game/Client/Server/Gpu 构建后被挪进 kernel 子目录
    //两处都要收集 否则指向它们的规则会被静默丢掉
    //主库与加载器自身不在列 常规模组不允许改写内核本体与加载流程
    public static List<string> CollectKernelAssemblies()
    {
        var names = new List<string>(EmbeddedAssemblyLoader.ListEmbeddedAssemblies());
        var kernelDirectory = Path.Combine(AppPaths.BaseDirectory, EmbeddedAssemblyLoader.KernelDirectoryName);
        if (!Directory.Exists(kernelDirectory))
            return names;

        foreach (var path in Directory.EnumerateFiles(kernelDirectory, "NetCraft.*.dll"))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (name is "NetCraft" or "NetCraft.Loader" or "NetCraft.ModLoader")
                continue;
            if (!names.Contains(name, StringComparer.OrdinalIgnoreCase))
                names.Add(name);
        }
        return names;
    }

    //PreloadTargets 把改写后的内核目标程序集抢先加载进 Default
    //解析回调也能改写 但那是按需触发的 预载一步到位不依赖时机
    //模组目标不走这里 它们由 ModAssemblies.Load 在加载那一刻改写
    //已在 Default 里的程序集改写无从插手 只能告警说这次注入赶不上了
    private static void PreloadTargets(ModHooks hooks, List<string> kernelNames)
    {
        var kernel = new HashSet<string>(kernelNames, StringComparer.Ordinal);
        foreach (var name in hooks.TargetAssemblies)
        {
            if (!kernel.Contains(name))
                continue;

            if (AssemblyLoadContext.Default.Assemblies.Any(a => a.GetName().Name == name))
            {
                Log.Warning($"Assembly {name} was already loaded early, its injection rules are too late");
                continue;
            }

            var bytes = EmbeddedAssemblyLoader.ReadAssemblyBytes(name);
            if (bytes is null)
            {
                Log.Warning($"Assembly {name} is neither embedded nor in the kernel directory, injection rules have nowhere to apply");
                continue;
            }

            bytes = hooks.Rewrite(name, bytes);
            AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(bytes));
            Log.Info($"Rewrote and preloaded assembly {name}");
        }
    }

    //ReadKernelAssembly 取内核程序集的原始字节
    //内嵌资源优先 kernel 目录兜底 返回 null 表示两处都没有
    public static byte[]? ReadKernelAssembly(string assemblyName)
        => EmbeddedAssemblyLoader.ReadAssemblyBytes(assemblyName);
}

//ModBootstrapResult 引导产物
//Hooks 注入装配结果 可查目标程序集与规则错误
//Manager 模组管理器 后续查询模组状态与退出时调 ShutdownAsync 都靠它
//Load 本次加载统计
public sealed class ModBootstrapResult
{
    public required ModHooks Hooks { get; init; }
    public required ModManager Manager { get; init; }
    public required LoadResult Load { get; init; }
}
