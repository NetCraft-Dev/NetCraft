namespace NetCraft.Registry;

//Registry access entry point; extends HolderLookupProvider to query registries by registry key and support freezing
public interface RegistryAccess : HolderLookupProvider
{
    //Empty empty registry access, used during Login/Handshake when there is no registry context
    public static RegistryAccess Empty { get; } = new ImmutableRegistryAccess(Array.Empty<RegistryEntry>());

    //Look up a registry by registry key; returns null if not found
    Registry<E>? Lookup<E>(ResourceKey<Registry<E>> registryKey) where E : class;

    //All registry entries
    IEnumerable<RegistryEntry> Registries { get; }

    //HolderLookupProvider.Lookup delegates directly to Lookup<E> since the signatures match
    Registry<T>? HolderLookupProvider.Lookup<T>(ResourceKey<Registry<T>> registryKey) where T : class
        => Lookup<T>(registryKey);

    //HolderLookupProvider.ListRegistryKeys extracts all registry identifiers from Registries
    IEnumerable<Identifier> HolderLookupProvider.ListRegistryKeys()
        => Registries.Select(e => e.Key);

    //Throws if the registry is not found
    Registry<E> LookupOrThrow<E>(ResourceKey<Registry<E>> registryKey) where E : class
    {
        var r = Lookup<E>(registryKey);
        if (r is null) throw new InvalidOperationException($"Missing registry: {registryKey}");
        return r;
    }

    //Freeze all registries and return an immutable RegistryAccess
    Frozen Freeze();
}

//Frozen RegistryAccess, a marker interface
public interface Frozen : RegistryAccess
{
}

//Registry entry binding a key and a value; vanilla uses a generic record
//Simplified: the value is stored as object and the caller casts to Registry<E> as needed
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

//Immutable RegistryAccess storing registries keyed by Identifier
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

    //ListRegistryKeys returns the dictionary keys directly, saving one Select projection versus the interface default
    public IEnumerable<Identifier> ListRegistryKeys() => _registries.Keys;

    public IEnumerable<RegistryEntry> Registries
        => _registries.Select(e => new RegistryEntry(e.Key, e.Value));

    public Frozen Freeze() => this;
}
