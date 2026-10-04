namespace NetCraft.Registry;

//值持有者对应原版Holder
//包装注册表元素区分Direct直接包装值与Reference注册表引用
public interface Holder<T> where T : class
{
    enum Kind { Reference, Direct }

    //持有的值
    T Value { get; }

    bool IsBound();
    bool AreComponentsBound();

    bool Is(Identifier key);
    bool Is(ResourceKey<T> key);
    bool Is(Predicate<ResourceKey<T>> predicate);
    bool Is(TagKey<T> tag);

    //所属种类
    Kind HolderKind { get; }

    //组件映射
    DataComponentMap Components { get; }

    //解包为键Reference返回key Direct返回null
    ResourceKey<T>? UnwrapKey();

    //是否可序列化进owner
    bool CanSerializeIn(HolderOwner<T> owner);

    //标签集合
    IEnumerable<TagKey<T>> Tags();

    //注册名未注册返回[unregistered]
    string RegisteredName => UnwrapKey()?.Identifier.ToString() ?? "[unregistered]";

    //直接包装值
    static Holder<T> Direct(T value) => new Direct<T>(value, DataComponentMap.Empty);

    //直接包装值带组件
    static Holder<T> Direct(T value, DataComponentMap components) => new Direct<T>(value, components);
}

//直接持有者包装值无注册表键
public sealed record Direct<T>(T Value, DataComponentMap Components) : Holder<T> where T : class
{
    public bool IsBound() => true;
    public bool AreComponentsBound() => true;

    public bool Is(Identifier key) => false;
    public bool Is(ResourceKey<T> key) => false;
    public bool Is(Predicate<ResourceKey<T>> predicate) => false;
    public bool Is(TagKey<T> tag) => false;

    public Holder<T>.Kind HolderKind => Holder<T>.Kind.Direct;

    public ResourceKey<T>? UnwrapKey() => null;

    public bool CanSerializeIn(HolderOwner<T> owner) => true;

    public IEnumerable<TagKey<T>> Tags() => Array.Empty<TagKey<T>>();

    public override string ToString() => $"Direct{{{Value}}}";

    //Deprecated按值比较
    public bool Is(Holder<T> holder) => Value.Equals(holder.Value);
}

//注册表引用持有者对应原版Holder.Reference
//可变bind方法在注册或冻结时调用
public sealed class Reference<T> : Holder<T> where T : class
{
    private readonly HolderOwner<T> _owner;
    private HashSet<TagKey<T>>? _tags;
    private DataComponentMap? _components;
    private readonly Type _type;
    private ResourceKey<T>? _key;
    private T? _value;

    private enum Type { StandAlone, Intrusive }

    private Reference(Type type, HolderOwner<T> owner, ResourceKey<T>? key, T? value)
    {
        _owner = owner;
        _type = type;
        _key = key;
        _value = value;
    }

    //创建独立引用先有key value待绑定
    public static Reference<T> CreateStandAlone(HolderOwner<T> owner, ResourceKey<T> key)
        => new(Type.StandAlone, owner, key, null);

    //创建侵入式引用先有value key待绑定 Deprecated
    public static Reference<T> CreateIntrusive(HolderOwner<T> owner, T value)
        => new(Type.Intrusive, owner, null, value);

    //键未绑定抛异常
    public ResourceKey<T> Key
    {
        get
        {
            if (_key is null)
                throw new InvalidOperationException($"Trying to access unbound value '{_value}' from registry {_owner}");
            return _key;
        }
    }

    public T Value
    {
        get
        {
            if (_value is null)
                throw new InvalidOperationException($"Trying to access unbound value '{_key}' from registry {_owner}");
            return _value;
        }
    }

    public bool IsBound() => _key is not null && _value is not null;
    public bool AreComponentsBound() => _components is not null;

    public bool Is(Identifier key) => Key.Identifier == key;

    public bool Is(ResourceKey<T> key) => ReferenceEquals(Key, key);

    public bool Is(Predicate<ResourceKey<T>> predicate) => predicate(Key);

    public bool Is(TagKey<T> tag) => BoundTags.Contains(tag);

    public bool Is(Holder<T> holder) => holder.Is(Key);

    public Holder<T>.Kind HolderKind => Holder<T>.Kind.Reference;

    public DataComponentMap Components
        => _components ?? throw new InvalidOperationException("Components not bound yet");

    public ResourceKey<T>? UnwrapKey() => Key;

    public bool CanSerializeIn(HolderOwner<T> context) => _owner.CanSerializeIn(context);

    public IEnumerable<TagKey<T>> Tags() => BoundTags;

    private HashSet<TagKey<T>> BoundTags
        => _tags ?? throw new InvalidOperationException("Tags not bound");

    public override string ToString() => $"Reference{{{_key}={_value}}}";

    //绑定方法注册或冻结时由MappedRegistry调用

    internal void BindKey(ResourceKey<T> key)
    {
        if (_key is not null && !ReferenceEquals(_key, key))
            throw new InvalidOperationException($"Can't change holder key: existing={_key}, new={key}");
        _key = key;
    }

    internal void BindValue(T value)
    {
        if (_type == Type.Intrusive && !ReferenceEquals(_value, value))
            throw new InvalidOperationException($"Can't change holder {_key} value: existing={_value}, new={value}");
        _value = value;
    }

    internal void BindTags(IEnumerable<TagKey<T>> tags)
    {
        _tags = new HashSet<TagKey<T>>(tags);
    }

    public void BindComponents(DataComponentMap components)
    {
        _components = components;
    }
}
