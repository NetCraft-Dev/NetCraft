using System.Collections.Concurrent;

namespace NetCraft.ModLoader;

//ModManager 模组管理器对应原版 Fabric Loader 的加载职责
//加载期一次性完成 运行期只读 不提供动态装卸与卸载
public sealed partial class ModManager
{
    private string _modsFolderPath = string.Empty;
    private readonly Dictionary<string, InternalModInfo> _mods = new();
    private readonly Dictionary<string, List<string>> _dependencyGraph = new();
    private readonly List<string> _loadOrder = new();
    private readonly ConcurrentDictionary<Type, object> _services = new();
    private readonly ProgressInfo _progress = new();

    //ModLoaded 模组初始化成功
    public event Action<ModInfo>? ModLoaded;
    //ModFailed 模组加载或初始化失败
    public event Action<ModInfo, Exception>? ModFailed;
    //ProgressUpdated 加载进度变化
    public event Action<ProgressInfo>? ProgressUpdated;
    //OnError 流程内异常
    public event Action<string, Exception>? OnError;
    //OnBeforeModLoad 加载前置 宿主可返回跳过或接管
    public event Func<ModLoadContext, Task<ModLoadAction>>? OnBeforeModLoad;
    //OnModInterrupted 宿主接管加载时的回调
    public event Func<ModLoadContext, Task<InterruptResult>>? OnModInterrupted;
    //OnModLoadComplete 单个模组流程结束
    public event Action<ModLoadContext, ModStatus>? OnModLoadComplete;

    //Progress 当前进度快照
    public ProgressInfo Progress => _progress;

    //Init 指定模组目录 目录不存在时自动创建
    public void Init(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            throw new ArgumentException("模组目录不能为空", nameof(folderPath));
        _modsFolderPath = Path.GetFullPath(folderPath);
        if (!Directory.Exists(_modsFolderPath))
            Directory.CreateDirectory(_modsFolderPath);
    }

    //RegisterService 登记供模组注入的服务实例
    public void RegisterService<T>(T service) where T : class => _services[typeof(T)] = service;

    //GetService 取已登记的服务
    public T? GetService<T>() where T : class
        => _services.TryGetValue(typeof(T), out var service) ? (T)service : null;

    //GetAllMods 全部模组信息
    public IReadOnlyList<ModInfo> GetAllMods() => _mods.Values.Select(m => m.ToPublic()).ToList();

    //GetAllModNames 全部模组名
    public IReadOnlyList<string> GetAllModNames() => _mods.Keys.ToList();

    //GetLoadedMods 已加载完成的模组信息
    public IReadOnlyList<ModInfo> GetLoadedMods()
        => _mods.Values.Where(m => m.Status == ModStatus.Running).Select(m => m.ToPublic()).ToList();

    //GetLoadedModNames 已加载完成的模组名
    public IReadOnlyList<string> GetLoadedModNames()
        => _mods.Values.Where(m => m.Status == ModStatus.Running).Select(m => m.Name).ToList();

    //GetStatus 查询模组状态 未收录时为 NotFound
    public ModStatus GetStatus(string name) => _mods.TryGetValue(name, out var mod) ? mod.Status : ModStatus.NotFound;

    //IsLoaded 模组是否加载完成
    public bool IsLoaded(string name) => GetStatus(name) == ModStatus.Running;

    //GetModInfo 查询模组详情
    public ModInfo? GetModInfo(string name) => _mods.TryGetValue(name, out var mod) ? mod.ToPublic() : null;

    //GetDependencies 模组依赖的模组名
    public IReadOnlyList<string> GetDependencies(string name)
        => _dependencyGraph.TryGetValue(name, out var deps) ? deps : Array.Empty<string>();

    //GetModsDependingOn 反向查询哪些模组依赖了它
    public IReadOnlyList<string> GetModsDependingOn(string name)
        => _dependencyGraph.Where(kv => kv.Value.Contains(name)).Select(kv => kv.Key).ToList();

    //GetLoadOrder 实际的初始化顺序 即拓扑排序结果
    public IReadOnlyList<string> GetLoadOrder() => _loadOrder;

    //ReadResource 按模组名读它内嵌的任意资源字节
    //界面取图标走这里 找不到或读不出来返回 null
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
            OnError?.Invoke($"模组 {name} 的资源 {resourceName} 读取失败", ex);
            return null;
        }
    }

    //ReadIcon 取模组图标字节
    //先按清单 icon 指的走 清单没写或指向的资源不在时退回内嵌资源里名为 icon.png 的那张
    //两处都没有返回 null 由界面自己决定用什么占位图
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

    //IsConventionalIcon 内嵌资源名是否命中约定的图标名
    //默认名可能被项目命名空间加了前缀 所以按后缀认
    private static bool IsConventionalIcon(string resourceName)
        => resourceName.Equals("icon.png", StringComparison.OrdinalIgnoreCase)
            || resourceName.EndsWith(".icon.png", StringComparison.OrdinalIgnoreCase);

    //ShutdownAsync 进程退出前调各模组的 Exit 让模组自行收尾
    //不做卸载 程序集与已注册内容保持到进程结束
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
                OnError?.Invoke($"模组 {mod.Name} 退出失败", ex);
            }
        }
    }

    //UpdateProgress 刷新进度并广播
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
