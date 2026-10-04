namespace NetCraft.Registry;

//注册表访问入口，继承 HolderLookupProvider 提供按注册表 key 查询注册表并支持冻结
public interface RegistryAccess : HolderLookupProvider
{
    //Empty 空注册表访问用于 Login/Handshake 阶段无注册表上下文
    public static RegistryAccess Empty { get; } = new ImmutableRegistryAccess(Array.Empty<RegistryEntry>());

    //按注册表 key 查注册表，找不到返回 null
    Registry<E>? Lookup<E>(ResourceKey<Registry<E>> registryKey) where E : class;

    //所有注册表条目
    IEnumerable<RegistryEntry> Registries { get; }

    //HolderLookupProvider.Lookup 直接委托 Lookup<E> 签名一致
    Registry<T>? HolderLookupProvider.Lookup<T>(ResourceKey<Registry<T>> registryKey) where T : class
        => Lookup<T>(registryKey);

    //HolderLookupProvider.ListRegistryKeys 从 Registries 提取所有注册表标识符
    IEnumerable<Identifier> HolderLookupProvider.ListRegistryKeys()
        => Registries.Select(e => e.Key);

    //找不到注册表抛异常
    Registry<E> LookupOrThrow<E>(ResourceKey<Registry<E>> registryKey) where E : class
    {
        var r = Lookup<E>(registryKey);
        if (r is null) throw new InvalidOperationException($"Missing registry: {registryKey}");
        return r;
    }

    //冻结所有注册表返回不可变 RegistryAccess
    Frozen Freeze();
}

//冻结的 RegistryAccess，标记接口
public interface Frozen : RegistryAccess
{
}

//注册表条目，绑定 key 与 value，原版为泛型 record
//简化：value 用 object 存储，调用方按需 cast 为 Registry<E>
public sealed class RegistryEntry
{
    public Identifier Key { get; }
    public object Value { get; }

    public RegistryEntry(Identifier key, object value)
    {
        Key = key;
        Value = value;
    }

    public override string ToString() => $"{Key}={Value}";
}

//不可变 RegistryAccess，以 Identifier 为键存注册表
public sealed class ImmutableRegistryAccess : Frozen
{
    private readonly Dictionary<Identifier, object> _registries;

    public ImmutableRegistryAccess(IEnumerable<KeyValuePair<Identifier, object>> registries)
    {
        _registries = new Dictionary<Identifier, object>(registries);
    }

    public ImmutableRegistryAccess(IEnumerable<RegistryEntry> entries)
    {
        _registries = entries.ToDictionary(e => e.Key, e => e.Value);
    }

    public Registry<E>? Lookup<E>(ResourceKey<Registry<E>> registryKey) where E : class
        => _registries.TryGetValue(registryKey.Identifier, out var r) ? r as Registry<E> : null;

    //ListRegistryKeys 直接返回字典键比接口默认实现少一次 Select 投影
    public IEnumerable<Identifier> ListRegistryKeys() => _registries.Keys;

    public IEnumerable<RegistryEntry> Registries
        => _registries.Select(e => new RegistryEntry(e.Key, e.Value));

    public Frozen Freeze() => this;
}
