namespace NetCraft.Registry;

//HolderLookup 元素查找接口对应原版 net.minecraft.core.HolderLookup
//Registry 继承提供按 ResourceKey 查 Holder 与列举元素标签能力
//故意不含 Get(Identifier)/Get(TagKey) 避免与 Registry 同名方法签名冲突 Registry 已有这两个方法返回更具体的 Reference/NamedHolderSet
public interface HolderLookup<T> where T : class
{
    //ListElements 列举所有已注册 Holder
    IEnumerable<Holder<T>> ListElements();

    //Get 按 ResourceKey 查 Holder 找不到返回 null
    Holder<T>? Get(ResourceKey<T> key);

    //ListTags 列举所有已绑定标签与对应 HolderSet
    IEnumerable<KeyValuePair<TagKey<T>, HolderSet<T>>> ListTags();

    //CanSerializeIn 判断 Holder 能否在指定 owner 上下文序列化
    bool CanSerializeIn(HolderOwner<T> owner);

    //GetOrDefault 按 ResourceKey 查 Holder 找不到返回 Direct(value) 或 null
    Holder<T>? GetOrDefault(ResourceKey<T> key, T? defaultValue)
    {
        var holder = Get(key);
        if (holder is not null) return holder;
        return defaultValue is not null ? Holder<T>.Direct(defaultValue) : null;
    }
}

//HolderLookupProvider 跨注册表查找入口对应原版 HolderLookup.Provider
//RegistryAccess 继承提供 lookup 按注册表 key 查 Registry
//用顶层接口名 HolderLookupProvider 避免嵌套命名冗长
public interface HolderLookupProvider
{
    //Lookup 按注册表 key 查 Registry 找不到返回 null
    Registry<T>? Lookup<T>(ResourceKey<Registry<T>> registryKey) where T : class;

    //ListRegistryKeys 所有注册表标识符
    IEnumerable<Identifier> ListRegistryKeys();

    //LookupOrThrow 按注册表 key 查 Registry 找不到抛异常
    Registry<T> LookupOrThrow<T>(ResourceKey<Registry<T>> registryKey) where T : class
    {
        var r = Lookup<T>(registryKey);
        if (r is null) throw new InvalidOperationException($"Missing registry: {registryKey}");
        return r;
    }
}
