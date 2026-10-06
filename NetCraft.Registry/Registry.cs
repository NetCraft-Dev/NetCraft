using NetCraft.Util.Random;

namespace NetCraft.Registry;

//Registry interface; extends IdMap + HolderLookup to provide key/id lookup plus element/tag enumeration
public interface Registry<T> : IdMap<T>, HolderLookup<T> where T : class
{
    //The registry's own key
    ResourceKey<Registry<T>> Key { get; }

    //Look up the registry name for a value; returns null if not found
    Identifier? GetKey(T thing);

    //Look up the ResourceKey for a value; returns null if not found
    ResourceKey<T>? GetResourceKey(T thing);

    //Look up a value by key; returns null if not found
    T? GetValue(ResourceKey<T> key);

    //Look up a value by registry name; returns null if not found
    T? GetValue(Identifier key);

    //Look up registration metadata
    RegistrationInfo? GetRegistrationInfo(ResourceKey<T> element);

    //Return an arbitrary element, the first one
    Reference<T>? GetAny();

    //All registry names
    IReadOnlyCollection<Identifier> KeySet { get; }

    //All element keys
    IReadOnlyCollection<ResourceKey<T>> RegistryKeySet { get; }

    //All key/value entries
    IEnumerable<KeyValuePair<ResourceKey<T>, T>> EntrySet { get; }

    bool ContainsKey(Identifier key);
    bool ContainsKey(ResourceKey<T> key);

    //Freeze the registry, forbidding further modification
    Registry<T> Freeze();

    //Look up a Holder by id; returns null if not found
    Reference<T>? Get(int id);

    //Look up a Holder by registry name; returns null if not found
    Reference<T>? Get(Identifier id);

    //Wrap a value as a Holder; returns Reference if registered, otherwise Direct
    Holder<T> WrapAsHolder(T value);

    //Look up a Named HolderSet by TagKey; returns null if not found
    NamedHolderSet<T>? Get(TagKey<T> tag);

    //Get or register a Named HolderSet by TagKey; returns the registered instance while unbound
    //Elements are decoded before tags are bound, so a bare Get returns null and a caller-created instance can never be bound afterwards
    NamedHolderSet<T> GetOrCreate(TagKey<T> tag);

    //All bound Named HolderSets
    IEnumerable<NamedHolderSet<T>> GetTags();

    //Whether it holds the tag
    bool Holds(TagKey<T> tag);

    //BindTags binds the TagKey-to-Holder-list mapping onto the matching Named HolderSet
    //Vanilla puts this on the WritableRegistry interface; simplified here into Registry and implemented by MappedRegistry
    void BindTags(IReadOnlyDictionary<TagKey<T>, IReadOnlyList<Holder<T>>> pendingTags);

    //GetRandom returns a random Holder by id; returns null for an empty registry
    Holder<T>? GetRandom(RandomSource random);

    //TODO component subsystem: ComponentLookup

    //Look up a value by registry name; returns null if not found
    T? GetOptional(Identifier key) => GetValue(key);

    //Look up a value by key; returns null if not found
    T? GetOptional(ResourceKey<T> key) => GetValue(key);

    //Look up a value by key; throws if not found
    T GetValueOrThrow(ResourceKey<T> key)
    {
        var v = GetValue(key);
        if (v is null) throw new InvalidOperationException($"Missing key in {Key}: {key}");
        return v;
    }

    //Register by string name
    static T Register(Registry<T> registry, string name, T value)
        => Register(registry, Identifier.Parse(name), value);

    //Register by registry name
    static T Register(Registry<T> registry, Identifier id, T value)
        => Register(registry, ResourceKey<T>.Create(registry.Key, id), value);

    //Register by key
    static T Register(Registry<T> registry, ResourceKey<T> key, T value)
    {
        if (registry is WritableRegistry<T> writable)
            writable.Register(key, value, RegistrationInfo.BuiltIn);
        else
            throw new ArgumentException($"Registry is not writable: {registry}");
        return value;
    }

    //Register and return the Holder
    static Reference<T> RegisterForHolder(Registry<T> registry, ResourceKey<T> key, T value)
    {
        if (registry is WritableRegistry<T> writable)
            return writable.Register(key, value, RegistrationInfo.BuiltIn);
        throw new ArgumentException($"Registry is not writable: {registry}");
    }

    //Register by registry name and return the Holder
    static Reference<T> RegisterForHolder(Registry<T> registry, Identifier location, T value)
        => RegisterForHolder(registry, ResourceKey<T>.Create(registry.Key, location), value);
}

//Writable registry interface, providing Register writes and pre-freeze queries
public interface WritableRegistry<T> : Registry<T> where T : class
{
    //Register a value, returning its Holder
    Reference<T> Register(ResourceKey<T> key, T value, RegistrationInfo registrationInfo);

    //Create an intrusive Holder, maps to vanilla createIntrusiveHolder. Deprecated
    //The value already holds a Reference from construction, reused by BindKey at Register
    Reference<T> CreateIntrusiveHolder(T value);

    //Whether it is empty
    bool IsEmpty { get; }

    //IsFrozen whether frozen; Register throws after freezing
    //Data-driven loading and tests must check before writing, so a whole batch of elements doesn't hit "already frozen"
    bool IsFrozen { get; }

    //CreateRegistrationLookup returns a HolderLookupProvider containing only the current registry
    //During Bootstrap registration, cross-registry lookup is assembled by the upper layer from each registry's Provider
    HolderLookupProvider CreateRegistrationLookup();
}
