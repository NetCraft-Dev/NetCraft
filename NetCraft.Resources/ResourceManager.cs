using NetCraft.Registry;

namespace NetCraft.Resources;

//ResourceManager 资源管理器对应原版 net.minecraft.server.packs.resources.ResourceManager
//按优先级合并多个 PackResources 提供统一的资源访问 API
public sealed class ResourceManager
{
    //packs 按优先级排序（数值小的优先）
    private readonly List<Pack> _packs = new();
    //按类型分组的资源包列表缓存
    private readonly Dictionary<PackType, List<PackResources>> _byType = new();

    public IReadOnlyList<Pack> Packs => _packs;

    //Reloaded 资源包增删或显式 Reload 完成后触发
    //ReloadableServerResources 订阅此事件在 Pack 变更后重新加载 Tags 等
    public event EventHandler<ReloadEventArgs>? Reloaded;

    //AddPack 添加一个资源包按 Priority 插入合适位置
    public void AddPack(Pack pack)
    {
        var inserted = false;
        for (int i = 0; i < _packs.Count; i++)
        {
            if (_packs[i].Priority > pack.Priority)
            {
                _packs.Insert(i, pack);
                inserted = true;
                break;
            }
        }
        if (!inserted)
        {
            _packs.Add(pack);
        }
        RebuildCache();
    }

    //RemovePack 按 id 移除资源包
    public bool RemovePack(Identifier id)
    {
        var removed = _packs.RemoveAll(p => p.Id == id) > 0;
        if (removed) RebuildCache();
        return removed;
    }

    //GetResource 按 type+location 获取最高优先级资源
    public Resource? GetResource(PackType type, Identifier location)
    {
        if (!_byType.TryGetValue(type, out var list)) return null;
        foreach (var pack in list)
        {
            //探测用流必须立刻释放 否则文件包下会一直占着文件句柄
            using (var probe = pack.GetResource(type, location))
            {
                if (probe != null)
                {
                    return new Resource(location, pack.PackId, () => pack.GetResource(type, location) ?? new MemoryStream());
                }
            }
        }
        return null;
    }

    //ListResources 列出所有资源包中匹配 namespace + pathPrefix 的资源
    public IEnumerable<Resource> ListResources(PackType type, string namespaceName, string pathPrefix)
    {
        if (!_byType.TryGetValue(type, out var list)) yield break;
        var seen = new HashSet<Identifier>();
        foreach (var pack in list)
        {
            pack.ListResources(type, namespaceName, pathPrefix, seen);
        }
        foreach (var id in seen)
        {
            var resource = GetResource(type, id);
            if (resource != null) yield return resource;
        }
    }

    //GetNamespaces 获取所有资源包的命名空间并集
    public ISet<string> GetNamespaces(PackType type)
    {
        var result = new HashSet<string>();
        if (_byType.TryGetValue(type, out var list))
        {
            foreach (var pack in list)
            {
                result.UnionWith(pack.GetNamespaces(type));
            }
        }
        return result;
    }

    //RebuildCache 重建类型缓存
    private void RebuildCache()
    {
        _byType.Clear();
        foreach (var type in Enum.GetValues<PackType>())
        {
            _byType[type] = _packs.Select(p => p.Resources).ToList();
        }
    }

    //Reload 重建缓存并触发 Reloaded 事件通知监听器重新加载
    //AddPack/RemovePack 后调用方显式调此方法触发 Tags 等数据驱动重载
    public void Reload()
    {
        RebuildCache();
        Reloaded?.Invoke(this, new ReloadEventArgs(DateTime.UtcNow));
    }
}

//ReloadEventArgs 资源重载事件参数携带时间戳供监听器日志
public sealed class ReloadEventArgs : EventArgs
{
    public DateTime Timestamp { get; }
    public ReloadEventArgs(DateTime timestamp) => Timestamp = timestamp;
}
