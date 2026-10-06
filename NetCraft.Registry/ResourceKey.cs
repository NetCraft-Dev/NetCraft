using System.Collections.Concurrent;

namespace NetCraft.Registry;

//Resource key, maps to vanilla ResourceKey
//Made of a Registry name and an Identifier path; globally interned for deduplication
//Vanilla uses a wildcard pool with an unchecked cast; a per-T pool here is more precise
public sealed class ResourceKey<T> : IEquatable<ResourceKey<T>> where T : class
{
    private static readonly ConcurrentDictionary<InternKey, ResourceKey<T>> _pool = new();

    //Root registry name minecraft:root, maps to vanilla Registries.ROOT_REGISTRY_NAME
    //Inlined to avoid a circular static dependency between ResourceKey and Registries
    internal static readonly Identifier RootRegistryName = Identifier.WithDefaultNamespace("root");

    //Owning registry name; minecraft:root for elements of the root registry
    public Identifier Registry { get; }

    //Path within the registry
    public Identifier Identifier { get; }

    private ResourceKey(Identifier registryName, Identifier identifier)
    {
        Registry = registryName;
        Identifier = identifier;
    }

    //Create an element key in the given registry
    public static ResourceKey<T> Create(ResourceKey<Registry<T>> registryName, Identifier location)
        => CreateInternal(registryName.Identifier, location);

    //Internal factory; get or create in the current T pool by registryName and identifier
    internal static ResourceKey<T> CreateInternal(Identifier registryName, Identifier identifier)
        => _pool.GetOrAdd(new InternKey(registryName, identifier), k => new ResourceKey<T>(k.Registry, k.Identifier));

    //Whether it belongs to a registry; compares registry names
    public bool IsFor(ResourceKey<Registry<T>> registry) => Registry == registry.Identifier;

    //Try to cast to the registry's target type
    public ResourceKey<E>? Cast<E>(ResourceKey<Registry<E>> registry)
        where E : class
        => Registry == registry.Identifier ? (ResourceKey<E>)(object)this : null;

    //Create a derived key in another registry by appending a suffix
    public ResourceKey<E> Dependent<E>(ResourceKey<Registry<E>> registryKey, string suffix)
        where E : class
        => ResourceKey<E>.CreateInternal(registryKey.Identifier, Identifier.WithSuffix(suffix));

    //Create a derived key in another registry by transforming the path
    public ResourceKey<E> Dependent<E>(ResourceKey<Registry<E>> registryKey, Func<string, string> decoration)
        where E : class
        => ResourceKey<E>.CreateInternal(registryKey.Identifier, Identifier.WithPath(decoration));

    //The key of the registry this key belongs to; registry is root and identifier is the registry name
    public ResourceKey<Registry<T>> RegistryKey() => ResourceKey<Registry<T>>.CreateInternal(RootRegistryName, Registry);

    public override string ToString() => $"ResourceKey[{Registry} / {Identifier}]";

    public bool Equals(ResourceKey<T>? other) => other is not null && Registry == other.Registry && Identifier == other.Identifier;
    public override bool Equals(object? obj) => obj is ResourceKey<T> o && Equals(o);
    public override int GetHashCode() => HashCode.Combine(Registry, Identifier);

    public static bool operator ==(ResourceKey<T>? left, ResourceKey<T>? right)
        => ReferenceEquals(left, right) || (left is not null && right is not null && left.Equals(right));
    public static bool operator !=(ResourceKey<T>? left, ResourceKey<T>? right) => !(left == right);

    //Interning pool key made of two Identifiers
    private sealed record InternKey(Identifier Registry, Identifier Identifier);
}
