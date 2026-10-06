namespace NetCraft.Registry;

//HolderLookup element lookup interface, maps to vanilla net.minecraft.core.HolderLookup
//Registry extends it to look up a Holder by ResourceKey and enumerate element tags
//Deliberately omits Get(Identifier)/Get(TagKey) to avoid signature conflicts with same-named Registry methods; Registry already has both and returns the more specific Reference/NamedHolderSet
public interface HolderLookup<T> where T : class
{
    //ListElements enumerates all registered Holders
    IEnumerable<Holder<T>> ListElements();

    //Get looks up a Holder by ResourceKey; returns null if not found
    Holder<T>? Get(ResourceKey<T> key);

    //ListTags enumerates all bound tags with their HolderSets
    IEnumerable<KeyValuePair<TagKey<T>, HolderSet<T>>> ListTags();

    //CanSerializeIn determines whether a Holder can be serialized in the given owner context
    bool CanSerializeIn(HolderOwner<T> owner);

    //GetOrDefault looks up a Holder by ResourceKey; falls back to Direct(value) or null
    Holder<T>? GetOrDefault(ResourceKey<T> key, T? defaultValue)
    {
        var holder = Get(key);
        if (holder is not null) return holder;
        return defaultValue is not null ? Holder<T>.Direct(defaultValue) : null;
    }
}

//HolderLookupProvider cross-registry lookup entry point, maps to vanilla HolderLookup.Provider
//RegistryAccess extends it to look up a Registry by registry key
//Uses the top-level interface name HolderLookupProvider to avoid verbose nesting
public interface HolderLookupProvider
{
    //Lookup looks up a Registry by registry key; returns null if not found
    Registry<T>? Lookup<T>(ResourceKey<Registry<T>> registryKey) where T : class;

    //ListRegistryKeys lists all registry identifiers
    IEnumerable<Identifier> ListRegistryKeys();

    //LookupOrThrow looks up a Registry by registry key; throws if not found
    Registry<T> LookupOrThrow<T>(ResourceKey<Registry<T>> registryKey) where T : class
    {
        var r = Lookup<T>(registryKey);
        if (r is null) throw new InvalidOperationException($"Missing registry: {registryKey}");
        return r;
    }
}
