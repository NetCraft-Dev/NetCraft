using NetCraft.Logging;
using NetCraft.Registry;

namespace NetCraft.Tags;

//ITagLoader 非泛型标记接口
//绕过 C# 泛型不变性 TagLoader<Block> 无法 cast 为 TagLoader<object>
//实际存取时通过此接口避免类型不兼容
public interface ITagLoader { }

//TagManager 全局标签管理器对应原版 net.minecraft.tags.TagManager
//持有各注册表的 TagLoader 与已构建结果BindAll 时遍历调 Registry.BindTags
//_loaders 用 Identifier 作键避免 C# 泛型不变性 ResourceKey<Registry<T>> 无法 cast 为 ResourceKey<Registry<object>>
public sealed class TagManager
{
    //loaders 各注册表的 TagLoader 实例按注册表 Identifier 索引
    private readonly Dictionary<Identifier, ITagLoader> _loaders = new();
    //binders 各注册表的绑定闭包捕获 T 类型完成 BuildAll→BindTags 转换
    private readonly List<Action<RegistryAccess>> _binders = new();

    //RegisterLoader 注册一个注册表的标签加载器与已构建结果
    //builtTags 是调用方先调 loader.LoadDirectory + BuildAll 拿到的结果
    public void RegisterLoader<T>(
        ResourceKey<Registry<T>> registryKey,
        TagLoader<T> loader,
        IReadOnlyDictionary<Identifier, List<T>> builtTags)
        where T : class
    {
        Log.Debug($"RegisterLoader entry registryKey={registryKey} loader={loader} builtTags={builtTags}");
        _loaders[registryKey.Identifier] = loader;
        _binders.Add(access => BindLoaderForRegistry(access, registryKey, builtTags));
        //Log.Debug($"RegisterLoader 出口");
    }

    //GetLoader 获取注册表的标签加载器
    public TagLoader<T>? GetLoader<T>(ResourceKey<Registry<T>> registryKey)
        where T : class
    {
        Log.Debug($"GetLoader entry registryKey={registryKey}");
        TagLoader<T>? result = null;
        if (_loaders.TryGetValue(registryKey.Identifier, out var loader) && loader is TagLoader<T> typed)
        {
            result = typed;
        }
        Log.Debug($"GetLoader exit result={result}");
        return result;
    }

    //BindLoaderForRegistry 把 builtTags 转换为 TagKey→Holder 列表调 Registry.BindTags
    //闭包捕获 T 类型避免 type erasure 后丢失泛型参数
    private static void BindLoaderForRegistry<T>(
        RegistryAccess access,
        ResourceKey<Registry<T>> registryKey,
        IReadOnlyDictionary<Identifier, List<T>> builtTags)
        where T : class
    {
        var registry = access.Lookup<T>(registryKey);
        if (registry is null) return;

        var pendingTags = new Dictionary<TagKey<T>, IReadOnlyList<Holder<T>>>();
        foreach (var (tagId, values) in builtTags)
        {
            var tagKey = TagKey<T>.Create(registryKey, tagId);
            var holders = values.Select(v => registry.WrapAsHolder(v)).ToList();
            pendingTags[tagKey] = holders;
        }
        registry.BindTags(pendingTags);
    }

    //BindAll 绑定所有注册表的标签到对应 Registry
    public void BindAll(RegistryAccess access)
    {
        Log.Debug($"BindAll entry access={access}");
        foreach (var binder in _binders)
            binder(access);
        //Log.Debug($"BindAll 出口");
    }

    //Reset 清空所有加载器和已构建标签
    public void Reset()
    {
        //Log.Debug($"Reset 入口");
        _loaders.Clear();
        _binders.Clear();
        //Log.Debug($"Reset 出口");
    }
}
