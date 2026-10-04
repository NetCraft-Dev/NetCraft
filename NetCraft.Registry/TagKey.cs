using System.Collections.Concurrent;

namespace NetCraft.Registry;

//标签键对应原版TagKey
//标识注册表内一个标签由Registry注册表键和Location路径组成
//全局intern去重相同registry和location返回同一实例
public sealed class TagKey<T> : IEquatable<TagKey<T>> where T : class
{
    private static readonly ConcurrentDictionary<InternKey, TagKey<T>> _pool = new();

    public ResourceKey<Registry<T>> Registry { get; }
    public Identifier Location { get; }

    private TagKey(ResourceKey<Registry<T>> registry, Identifier location)
    {
        Registry = registry;
        Location = location;
    }

    //创建并intern去重
    public static TagKey<T> Create(ResourceKey<Registry<T>> registry, Identifier location)
        => _pool.GetOrAdd(new InternKey(registry, location), _ => new TagKey<T>(registry, location));

    //判断是否属于registry引用比较依赖ResourceKey intern
    public bool IsFor(ResourceKey<Registry<T>> registry) => ReferenceEquals(Registry, registry);

    //尝试转型为另一注册表类型
    public TagKey<E>? Cast<E>(ResourceKey<Registry<E>> registry)
        where E : class
        => IsFor((ResourceKey<Registry<T>>)(object)registry) ? (TagKey<E>)(object)this : null;

    public override string ToString() => $"TagKey[{Registry.Identifier} / {Location}]";

    public bool Equals(TagKey<T>? other) => other is not null && ReferenceEquals(Registry, other.Registry) && Location == other.Location;
    public override bool Equals(object? obj) => obj is TagKey<T> o && Equals(o);
    public override int GetHashCode() => HashCode.Combine(Registry, Location);

    public static bool operator ==(TagKey<T>? left, TagKey<T>? right)
        => ReferenceEquals(left, right) || (left is not null && right is not null && left.Equals(right));
    public static bool operator !=(TagKey<T>? left, TagKey<T>? right) => !(left == right);

    private sealed record InternKey(ResourceKey<Registry<T>> Registry, Identifier Location);
}
