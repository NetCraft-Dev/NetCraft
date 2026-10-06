namespace NetCraft.Registry;

//Factory methods spanning Registry; a standalone non-generic class avoids nesting T
public static class ResourceKeys
{
    //Creates the registry's own key; registry equals root
    public static ResourceKey<Registry<T>> CreateRegistryKey<T>(Identifier identifier) where T : class
        => ResourceKey<Registry<T>>.CreateInternal(ResourceKey<T>.RootRegistryName, identifier);
}
