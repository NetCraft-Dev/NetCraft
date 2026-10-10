using System.Collections.Frozen;
using System.Collections.ObjectModel;
using NetCraft.Config;
using NetCraft.Logging;
using NetCraft.Util.Random;

namespace NetCraft.Registry;

//Core registry implementation maintaining 6 lookup maps: byId/byLocation/byKey/byValue/toId/registrationInfos
//Optimization 2.3: build FrozenDictionary indexes after Freeze to speed up read-only lookups (RegistryFrozenDictionary switch)
public class MappedRegistry<T> : WritableRegistry<T>, HolderOwner<T> where T : class
{
    private readonly ResourceKey<Registry<T>> _key;
    private readonly List<Reference<T>> _byId = new();
    private readonly Dictionary<T, int> _toId = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Identifier, Reference<T>> _byLocation = new();
    private readonly Dictionary<ResourceKey<T>, Reference<T>> _byKey = new();
    private readonly Dictionary<T, Reference<T>> _byValue = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ResourceKey<T>, RegistrationInfo> _registrationInfos = new();
    private readonly Dictionary<TagKey<T>, NamedHolderSet<T>> _allTags = new();
    //Intrusive Holder cache, looked up by value reference; maps to vanilla intrusive holders
    private readonly Dictionary<T, Reference<T>> _intrusiveHolders = new(ReferenceEqualityComparer.Instance);
    private Lifecycle _registryLifecycle;
    private bool _frozen;
    //Frozen indexes for optimization 2.3; read-only lookups go through FrozenDictionary after freeze
    private FrozenDictionary<Identifier, Reference<T>>? _byLocationFrozen;
    private FrozenDictionary<ResourceKey<T>, Reference<T>>? _byKeyFrozen;
    private FrozenDictionary<T, int>? _toIdFrozen;
    private FrozenDictionary<T, Reference<T>>? _byValueFrozen;
    private FrozenDictionary<TagKey<T>, NamedHolderSet<T>>? _allTagsFrozen;

    public MappedRegistry(ResourceKey<Registry<T>> key, Lifecycle lifecycle)
    {
        _key = key;
        _registryLifecycle = lifecycle;
    }

    public ResourceKey<Registry<T>> Key => _key;

    //IsFrozen whether frozen; no more elements can be written after freezing
    public bool IsFrozen => _frozen;

    public Lifecycle RegistryLifecycle => _registryLifecycle;

    public override string ToString() => $"Registry[{_key} ({_registryLifecycle})]";

    private void ValidateWrite(ResourceKey<T> key)
    {
        if (_frozen)
            throw new InvalidOperationException($"Registry is already frozen (trying to add key {key})");
    }

    //Register a value using createStandAlone mode; the value is bound at Freeze
    //If the value already holds a Reference from CreateIntrusiveHolder, reuse it and BindKey
    public virtual Reference<T> Register(ResourceKey<T> key, T value, RegistrationInfo registrationInfo)
    {
        //Log.Debug($"Register enter key={key} value={value} registrationInfo={registrationInfo}");
        ValidateWrite(key);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        if (_byLocation.ContainsKey(key.Identifier))
            throw new InvalidOperationException($"Adding duplicate key '{key}' to registry");
        if (_byValue.ContainsKey(value))
            throw new InvalidOperationException($"Adding duplicate value '{value}' to registry");

        Reference<T> holder;
        if (_byKey.TryGetValue(key, out var existing))
        {
            holder = existing;
        }
        else if (_intrusiveHolders.TryGetValue(value, out var intrusive))
        {
            holder = intrusive;
            holder.BindKey(key);
        }
        else
        {
            holder = Reference<T>.CreateStandAlone(this, key);
        }
        holder.BindValue(value);

        _byKey[key] = holder;
        _byLocation[key.Identifier] = holder;
        _byValue[value] = holder;
        var newId = _byId.Count;
        _byId.Add(holder);
        _toId[value] = newId;
        _registrationInfos[key] = registrationInfo;
        _registryLifecycle = _registryLifecycle.Add(registrationInfo.Lifecycle);
        //Log.Debug($"Register exit result={holder}");
        return holder;
    }

    //CreateIntrusiveHolder creates an intrusive Holder, maps to vanilla createIntrusiveHolder
    //The value holds a Reference from construction, reused by BindKey at Register
    public Reference<T> CreateIntrusiveHolder(T value)
    {
        //Log.Debug($"CreateIntrusiveHolder enter value={value}");
        ArgumentNullException.ThrowIfNull(value);
        if (_intrusiveHolders.TryGetValue(value, out var existing))
        {
            //Log.Debug($"CreateIntrusiveHolder exit result={existing}");
            return existing;
        }
        var holder = Reference<T>.CreateIntrusive(this, value);
        _intrusiveHolders[value] = holder;
        //Log.Debug($"CreateIntrusiveHolder exit result={holder}");
        return holder;
    }

    public virtual Identifier? GetKey(T thing)
        => TryGetByValue(thing, out var holder) ? holder!.Key.Identifier : null;

    public ResourceKey<T>? GetResourceKey(T thing)
        => TryGetByValue(thing, out var holder) ? holder!.Key : null;

    public virtual int GetId(T thing)
        => TryGetToId(thing, out var id) ? id : IdMap<T>.Default;

    public virtual T? ById(int id)
    {
        if ((uint)id >= (uint)_byId.Count) return null;
        return _byId[id].Value;
    }

    public Reference<T>? Get(int id)
        => (uint)id < (uint)_byId.Count ? _byId[id] : null;

    public Reference<T>? Get(Identifier id)
        => TryGetByLocation(id, out var holder) ? holder : null;

    public virtual Reference<T>? GetAny() => _byId.Count == 0 ? null : _byId[0];

    //GetRandom returns a random Holder by id; returns null for an empty registry
    public Holder<T>? GetRandom(RandomSource random)
        => _byId.Count == 0 ? null : _byId[random.NextInt(_byId.Count)];

    public T? GetValue(ResourceKey<T> key)
        => TryGetByKey(key, out var holder) ? holder!.Value : null;

    public virtual T? GetValue(Identifier key)
        => TryGetByLocation(key, out var holder) ? holder!.Value : null;

    public Holder<T> WrapAsHolder(T value)
    {
        if (TryGetByValue(value, out var holder)) return holder!;
        NetCraft.Util.SiteCounters.CountMappedRegistryMiss();
        return Holder<T>.Direct(value);
    }

    public RegistrationInfo? GetRegistrationInfo(ResourceKey<T> element)
        => _registrationInfos.TryGetValue(element, out var info) ? info : null;

    public int Size => _byKey.Count;

    public bool IsEmpty => _byKey.Count == 0;

    public IReadOnlyCollection<Identifier> KeySet => _byLocation.Keys.ToArray();

    public IReadOnlyCollection<ResourceKey<T>> RegistryKeySet => _byKey.Keys.ToArray();

    public IEnumerable<KeyValuePair<ResourceKey<T>, T>> EntrySet
        => _byKey.Select(e => new KeyValuePair<ResourceKey<T>, T>(e.Key, e.Value.Value));

    public bool ContainsKey(Identifier key)
        => _byLocationFrozen is not null ? _byLocationFrozen.ContainsKey(key) : _byLocation.ContainsKey(key);

    public bool ContainsKey(ResourceKey<T> key)
        => _byKeyFrozen is not null ? _byKeyFrozen.ContainsKey(key) : _byKey.ContainsKey(key);

    //Freeze the registry, binding values to Holders and validating unbound entries
    //Optimization 2.3: when the switch is enabled, build FrozenDictionary indexes to speed up later read-only lookups
    public Registry<T> Freeze()
    {
        //Log.Debug($"Freeze enter");
        if (_frozen)
        {
            Log.Debug($"Freeze exit result={this}");
            return this;
        }
        _frozen = true;
        foreach (var (value, holder) in _byValue)
            holder.BindValue(value);
        var unbound = _byKey
            .Where(e => !e.Value.IsBound())
            .Select(e => e.Key.Identifier.ToString())
            .OrderBy(s => s)
            .ToList();
        if (unbound.Count > 0)
            throw new InvalidOperationException($"Unbound values in registry {Key}: [{string.Join(", ", unbound)}]");
        //Log.Debug($"Step 1 build FrozenDictionary indexes");
        _byLocationFrozen = _byLocation.ToFrozenDictionary();
        _byKeyFrozen = _byKey.ToFrozenDictionary();
        _toIdFrozen = _toId.ToFrozenDictionary();
        _byValueFrozen = _byValue.ToFrozenDictionary();
        _allTagsFrozen = _allTags.ToFrozenDictionary();
        //TODO component: build componentLookup
        Log.Debug($"Freeze exit result={this}");
        return this;
    }

    //Frozen lookup helpers for optimization 2.3
    //When the Frozen indexes are built, use FrozenDictionary; otherwise fall back to Dictionary to keep semantics identical
    private bool TryGetByLocation(Identifier id, out Reference<T>? holder)
    {
        if (_byLocationFrozen is not null)
        {
            return _byLocationFrozen.TryGetValue(id, out holder);
        }
        return _byLocation.TryGetValue(id, out holder);
    }

    private bool TryGetByKey(ResourceKey<T> key, out Reference<T>? holder)
    {
        if (_byKeyFrozen is not null)
        {
            return _byKeyFrozen.TryGetValue(key, out holder);
        }
        return _byKey.TryGetValue(key, out holder);
    }

    private bool TryGetByValue(T value, out Reference<T>? holder)
    {
        if (_byValueFrozen is not null)
        {
            return _byValueFrozen.TryGetValue(value, out holder);
        }
        return _byValue.TryGetValue(value, out holder);
    }

    private bool TryGetToId(T value, out int id)
    {
        if (_toIdFrozen is not null)
        {
            return _toIdFrozen.TryGetValue(value, out id);
        }
        return _toId.TryGetValue(value, out id);
    }

    //GetOrCreateTagForRegistration gets or creates a Named HolderSet by TagKey
    private NamedHolderSet<T> GetOrCreateTagForRegistration(TagKey<T> tag)
    {
        if (!_allTags.TryGetValue(tag, out var named))
        {
            named = new NamedHolderSet<T>(this, tag);
            _allTags[tag] = named;
        }
        return named;
    }

    //BindTags binds the TagKey-to-Holder-list mapping onto Named HolderSets and refreshes each Reference's tag cache
    public void BindTags(IReadOnlyDictionary<TagKey<T>, IReadOnlyList<Holder<T>>> pendingTags)
    {
        Log.Debug($"BindTags entry pendingTags={pendingTags}");
        if (!_frozen)
            throw new InvalidOperationException("Registry is not frozen yet, cannot bind tags");

        foreach (var (tag, values) in pendingTags)
        {
            var named = GetOrCreateTagForRegistration(tag);
            named.Bind(values);
        }

        //Tags with no file in the data pack are still bound as empty sets, maps to vanilla tagMap.getOrDefault(key, List.of())
        //Leaving them unbound would make structures referencing them throw during generation; vanilla semantics are that no entries means an empty set
        foreach (var (_, pending) in _allTags)
        {
            if (!pending.IsBound) pending.Bind(Array.Empty<Holder<T>>());
        }

        var tagsForElement = new Dictionary<Reference<T>, List<TagKey<T>>>(ReferenceEqualityComparer.Instance);
        foreach (var holder in _byValue.Values)
            tagsForElement[holder] = new List<TagKey<T>>();

        foreach (var (tag, named) in _allTags)
        {
            if (!named.IsBound) continue;
            foreach (var holder in named)
            {
                if (holder is Reference<T> reference)
                    tagsForElement[reference].Add(tag);
            }
        }

        foreach (var (reference, tags) in tagsForElement)
            reference.BindTags(tags);

        //_allTags changes after BindTags, so the Frozen index is rebuilt
        //Log.Debug($"Step 1 rebuild _allTagsFrozen index");
        _allTagsFrozen = _allTags.ToFrozenDictionary();
        //Log.Debug($"BindTags exit");
    }

    //Get returns the bound set by TagKey
    //Tag lookup is a hot path for block/item checks; two log lines per call would instantly flood the log buffer, so no logging here on purpose
    public NamedHolderSet<T>? Get(TagKey<T> tag)
    {
        if (_allTagsFrozen is not null)
            return _allTagsFrozen.TryGetValue(tag, out var named) && named.IsBound ? named : null;
        return _allTags.TryGetValue(tag, out var named2) && named2.IsBound ? named2 : null;
    }

    //GetOrCreate gets or registers the instance by TagKey, maps to vanilla MappedRegistry.getOrCreateTag
    //Get only accepts bound tags; during element decoding tags are not yet bound, so returning null would make callers create their own instance that never gets bound
    public NamedHolderSet<T> GetOrCreate(TagKey<T> tag) => GetOrCreateTagForRegistration(tag);

    public IEnumerable<NamedHolderSet<T>> GetTags()
        => _allTags.Values.Where(n => n.IsBound);

    //Holds whether the TagKey is bound; same hot path as above, no logging
    public bool Holds(TagKey<T> tag)
    {
        if (_allTagsFrozen is not null)
            return _allTagsFrozen.TryGetValue(tag, out var named) && named.IsBound;
        return _allTags.TryGetValue(tag, out var named2) && named2.IsBound;
    }

    //HolderLookup.ListElements enumerates all registered Reference Holders
    public IEnumerable<Holder<T>> ListElements()
        => _byValue.Values.AsEnumerable();

    //HolderLookup.Get looks up a Holder by ResourceKey; returns null if not found
    public virtual Holder<T>? Get(ResourceKey<T> key)
        => TryGetByKey(key, out var holder) ? holder! : null;

    //HolderLookup.ListTags enumerates all bound tags with their HolderSets, reusing GetTags so frozen and dict paths stay consistent
    public IEnumerable<KeyValuePair<TagKey<T>, HolderSet<T>>> ListTags()
        => GetTags().Select(n => new KeyValuePair<TagKey<T>, HolderSet<T>>(n.Key, n));

    //HolderLookup.CanSerializeIn only the same registry instance can serialize
    public bool CanSerializeIn(HolderOwner<T> owner) => ReferenceEquals(this, owner);

    //CreateRegistrationLookup returns a HolderLookupProvider containing only the current registry
    //Matches by Identifier then casts to Registry<E>; failure throws InvalidCastException to match vanilla type-erasure semantics
    public HolderLookupProvider CreateRegistrationLookup()
        => new SingleRegistryLookupProvider<T>(this);

    public IEnumerator<T> GetEnumerator() => _byId.Select(h => h.Value).GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

//Single-registry HolderLookupProvider that can only look up the registry passed at construction
//Used for cross-registry lookup during Bootstrap registration, assembled by the upper layer from each registry's Provider
internal sealed class SingleRegistryLookupProvider<TRegistry> : HolderLookupProvider where TRegistry : class
{
    private readonly Registry<TRegistry> _registry;
    private readonly Identifier _registryId;

    public SingleRegistryLookupProvider(Registry<TRegistry> registry)
    {
        _registry = registry;
        _registryId = registry.Key.Identifier;
    }

    public Registry<T>? Lookup<T>(ResourceKey<Registry<T>> registryKey) where T : class
        => registryKey.Identifier == _registryId ? (Registry<T>)(object)_registry : null;

    public IEnumerable<Identifier> ListRegistryKeys() => new[] { _registryId };
}
