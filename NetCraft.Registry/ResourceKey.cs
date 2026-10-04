using System.Collections.Concurrent;

namespace NetCraft.Registry;

//资源键对应原版ResourceKey
//由Registry注册表名和Identifier路径组成，全局intern去重
//原版用通配符池加unchecked cast此处用per-T池语义更精确
public sealed class ResourceKey<T> : IEquatable<ResourceKey<T>> where T : class
{
    private static readonly ConcurrentDictionary<InternKey, ResourceKey<T>> _pool = new();

    //根注册表名minecraft:root对应原版Registries.ROOT_REGISTRY_NAME
    //内联避免ResourceKey与Registries循环静态依赖
    internal static readonly Identifier RootRegistryName = Identifier.WithDefaultNamespace("root");

    //所属注册表名root注册表元素时为minecraft:root
    public Identifier Registry { get; }

    //注册表内路径
    public Identifier Identifier { get; }

    private ResourceKey(Identifier registryName, Identifier identifier)
    {
        Registry = registryName;
        Identifier = identifier;
    }

    //在指定注册表内创建元素键
    public static ResourceKey<T> Create(ResourceKey<Registry<T>> registryName, Identifier location)
        => CreateInternal(registryName.Identifier, location);

    //内部工厂按registryName和identifier在当前T池中取或建
    internal static ResourceKey<T> CreateInternal(Identifier registryName, Identifier identifier)
        => _pool.GetOrAdd(new InternKey(registryName, identifier), k => new ResourceKey<T>(k.Registry, k.Identifier));

    //判断是否属于registry比较registry名
    public bool IsFor(ResourceKey<Registry<T>> registry) => Registry == registry.Identifier;

    //尝试转型为registry对应类型
    public ResourceKey<E>? Cast<E>(ResourceKey<Registry<E>> registry)
        where E : class
        => Registry == registry.Identifier ? (ResourceKey<E>)(object)this : null;

    //在另一注册表内创建派生键追加后缀
    public ResourceKey<E> Dependent<E>(ResourceKey<Registry<E>> registryKey, string suffix)
        where E : class
        => ResourceKey<E>.CreateInternal(registryKey.Identifier, Identifier.WithSuffix(suffix));

    //在另一注册表内创建派生键变换path
    public ResourceKey<E> Dependent<E>(ResourceKey<Registry<E>> registryKey, Func<string, string> decoration)
        where E : class
        => ResourceKey<E>.CreateInternal(registryKey.Identifier, Identifier.WithPath(decoration));

    //该键所属注册表的键registry为root identifier为registry名
    public ResourceKey<Registry<T>> RegistryKey() => ResourceKey<Registry<T>>.CreateInternal(RootRegistryName, Registry);

    public override string ToString() => $"ResourceKey[{Registry} / {Identifier}]";

    public bool Equals(ResourceKey<T>? other) => other is not null && Registry == other.Registry && Identifier == other.Identifier;
    public override bool Equals(object? obj) => obj is ResourceKey<T> o && Equals(o);
    public override int GetHashCode() => HashCode.Combine(Registry, Identifier);

    public static bool operator ==(ResourceKey<T>? left, ResourceKey<T>? right)
        => ReferenceEquals(left, right) || (left is not null && right is not null && left.Equals(right));
    public static bool operator !=(ResourceKey<T>? left, ResourceKey<T>? right) => !(left == right);

    //intern池key由两个Identifier组成
    private sealed record InternKey(Identifier Registry, Identifier Identifier);
}
