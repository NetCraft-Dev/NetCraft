namespace NetCraft.Registry;

//Value holder, maps to vanilla Holder
//Wraps a registry element; distinguishes Direct values from Reference registry references
public interface Holder<T> where T : class
{
    enum Kind { Reference, Direct }

    //Held value
    T Value { get; }

    bool IsBound();
    bool AreComponentsBound();

    bool Is(Identifier key);
    bool Is(ResourceKey<T> key);
    bool Is(Predicate<ResourceKey<T>> predicate);
    bool Is(TagKey<T> tag);

    //Holder kind
    Kind HolderKind { get; }

    //Component map
    DataComponentMap Components { get; }

    //Unwrap to a key; Reference returns the key and Direct returns null
    ResourceKey<T>? UnwrapKey();

    //Whether it can be serialized into owner
    bool CanSerializeIn(HolderOwner<T> owner);

    //Tag set
    IEnumerable<TagKey<T>> Tags();

    //Registry name; returns [unregistered] when unregistered
    string RegisteredName => UnwrapKey()?.Identifier.ToString() ?? "[unregistered]";

    //Directly wrap a value
    static Holder<T> Direct(T value) => new Direct<T>(value, DataComponentMap.Empty);

    //Directly wrap a value with components
    static Holder<T> Direct(T value, DataComponentMap components) => new Direct<T>(value, components);
}

//Direct holder wrapping a value with no registry key
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

    //Deprecated: compare by value
    public bool Is(Holder<T> holder) => Value.Equals(holder.Value);
}

//Registry reference holder, maps to vanilla Holder.Reference
//Mutable bind methods called at registration or freeze
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

    //Create a standalone reference; key comes first and value is bound later
    public static Reference<T> CreateStandAlone(HolderOwner<T> owner, ResourceKey<T> key)
        => new(Type.StandAlone, owner, key, null);

    //Create an intrusive reference; value comes first and key is bound later. Deprecated
    public static Reference<T> CreateIntrusive(HolderOwner<T> owner, T value)
        => new(Type.Intrusive, owner, null, value);

    //Throws if the key is unbound
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

    //Bind methods called by MappedRegistry on registration or freeze

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
