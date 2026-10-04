using NetCraft.Util.Random;

namespace NetCraft.Registry;

//注册表接口，继承 IdMap + HolderLookup 提供 key/id 查询与元素/标签列举
public interface Registry<T> : IdMap<T>, HolderLookup<T> where T : class
{
    //注册表自身的键
    ResourceKey<Registry<T>> Key { get; }

    //查值对应的注册名，找不到返回 null
    Identifier? GetKey(T thing);

    //查值对应的 ResourceKey，找不到返回 null
    ResourceKey<T>? GetResourceKey(T thing);

    //按 key 查值，找不到返回 null
    T? GetValue(ResourceKey<T> key);

    //按注册名查值，找不到返回 null
    T? GetValue(Identifier key);

    //查注册元信息
    RegistrationInfo? GetRegistrationInfo(ResourceKey<T> element);

    //任取一个元素，取首个
    Reference<T>? GetAny();

    //所有注册名
    IReadOnlyCollection<Identifier> KeySet { get; }

    //所有元素键
    IReadOnlyCollection<ResourceKey<T>> RegistryKeySet { get; }

    //所有键值条目
    IEnumerable<KeyValuePair<ResourceKey<T>, T>> EntrySet { get; }

    bool ContainsKey(Identifier key);
    bool ContainsKey(ResourceKey<T> key);

    //冻结注册表，禁止后续修改
    Registry<T> Freeze();

    //按 ID 查 Holder，找不到返回 null
    Reference<T>? Get(int id);

    //按注册名查 Holder，找不到返回 null
    Reference<T>? Get(Identifier id);

    //包装值为 Holder，已注册返回 Reference，否则返回 Direct
    Holder<T> WrapAsHolder(T value);

    //按TagKey查Named HolderSet找不到返回null
    NamedHolderSet<T>? Get(TagKey<T> tag);

    //按TagKey取或登记Named HolderSet未绑定时返回登记的那个实例
    //元素解码早于标签绑定只查Get会拿到null调用方自建实例后就再也绑不上
    NamedHolderSet<T> GetOrCreate(TagKey<T> tag);

    //所有已绑定Named HolderSet
    IEnumerable<NamedHolderSet<T>> GetTags();

    //是否含该标签
    bool Holds(TagKey<T> tag);

    //BindTags把TagKey到Holder列表的映射绑定到对应Named HolderSet
    //原版在WritableRegistry接口C#简化放Registry由MappedRegistry实现
    void BindTags(IReadOnlyDictionary<TagKey<T>, IReadOnlyList<Holder<T>>> pendingTags);

    //GetRandom 按 id 随机返回一个 Holder 空注册表返回 null
    Holder<T>? GetRandom(RandomSource random);

    //TODO component 子系统：ComponentLookup

    //按注册名查值，找不到返回 null
    T? GetOptional(Identifier key) => GetValue(key);

    //按 key 查值，找不到返回 null
    T? GetOptional(ResourceKey<T> key) => GetValue(key);

    //按 key 查值，找不到抛异常
    T GetValueOrThrow(ResourceKey<T> key)
    {
        var v = GetValue(key);
        if (v is null) throw new InvalidOperationException($"Missing key in {Key}: {key}");
        return v;
    }

    //按字符串名注册
    static T Register(Registry<T> registry, string name, T value)
        => Register(registry, Identifier.Parse(name), value);

    //按注册名注册
    static T Register(Registry<T> registry, Identifier id, T value)
        => Register(registry, ResourceKey<T>.Create(registry.Key, id), value);

    //按 key 注册
    static T Register(Registry<T> registry, ResourceKey<T> key, T value)
    {
        if (registry is WritableRegistry<T> writable)
            writable.Register(key, value, RegistrationInfo.BuiltIn);
        else
            throw new ArgumentException($"Registry is not writable: {registry}");
        return value;
    }

    //注册并返回 Holder
    static Reference<T> RegisterForHolder(Registry<T> registry, ResourceKey<T> key, T value)
    {
        if (registry is WritableRegistry<T> writable)
            return writable.Register(key, value, RegistrationInfo.BuiltIn);
        throw new ArgumentException($"Registry is not writable: {registry}");
    }

    //按注册名注册并返回 Holder
    static Reference<T> RegisterForHolder(Registry<T> registry, Identifier location, T value)
        => RegisterForHolder(registry, ResourceKey<T>.Create(registry.Key, location), value);
}

//可写注册表接口，提供 Register 写入与冻结前查询
public interface WritableRegistry<T> : Registry<T> where T : class
{
    //注册一个值，返回其 Holder
    Reference<T> Register(ResourceKey<T> key, T value, RegistrationInfo registrationInfo);

    //创建侵入式 Holder 对应原版 createIntrusiveHolder Deprecated
    //value 在构造时即持有 Reference 待 Register 时 BindKey 复用
    Reference<T> CreateIntrusiveHolder(T value);

    //是否为空
    bool IsEmpty { get; }

    //IsFrozen 是否已冻结 冻结后 Register 会抛异常
    //数据驱动加载与测试要在写入前判断 免得整批元素都撞上"already frozen"
    bool IsFrozen { get; }

    //CreateRegistrationLookup 返回只含当前注册表的 HolderLookupProvider
    //Bootstrap 注册期需要跨注册表查找时由上层汇总各注册表 Provider 实现
    HolderLookupProvider CreateRegistrationLookup();
}
