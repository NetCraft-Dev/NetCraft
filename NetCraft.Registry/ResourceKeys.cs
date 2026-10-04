namespace NetCraft.Registry;

//跨 Registry 的工厂方法，独立非泛型类避免 T 嵌套
public static class ResourceKeys
{
    //创建注册表自身的键registry等于root
    public static ResourceKey<Registry<T>> CreateRegistryKey<T>(Identifier identifier) where T : class
        => ResourceKey<Registry<T>>.CreateInternal(ResourceKey<T>.RootRegistryName, identifier);
}
