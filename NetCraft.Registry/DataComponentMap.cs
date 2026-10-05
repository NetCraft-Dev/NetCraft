using NetCraft.Codec;

namespace NetCraft.Registry;

//DataComponentMap 数据组件映射对应原版 net.minecraft.core.component.DataComponentMap
//继承 DataComponentLookup 只读接口加 Composite 静态构造 Builder 在 Network 子库构建可变版本
public interface DataComponentMap : DataComponentLookup
{
    //Empty 空映射单例
    public static DataComponentMap Empty { get; } = EmptyDataComponentMap.Instance;

    //CODEC 整表持久化编解码 transient 组件不写出 对应原版 DataComponentMap.CODEC
    public static readonly Codec<DataComponentMap> CODEC = DataComponentType<object>.VALUE_MAP_CODEC.ComapFlatMap(
        map =>
        {
            var builder = new DataComponentMapBuilder();
            foreach (var kv in map) builder.SetUnchecked(kv.Key, kv.Value);
            return DataResult<DataComponentMap>.Success(builder.Build());
        },
        ToValueMap);

    //Composite 组合 prototype 与 overrides overrides 优先
    static DataComponentMap Composite(DataComponentMap prototype, DataComponentMap overrides)
        => new CompositeDataComponentMap(prototype, overrides);

    //Builder 造组件映射构造器 对应原版 builder
    static DataComponentMapBuilder Builder() => new();

    //ToValueMap 取可持久化的组件项 编码侧过滤 transient 对应原版 makeCodecFromMap 的编码侧
    private static Dictionary<DataComponentType<object>, object> ToValueMap(DataComponentMap map)
    {
        var result = new Dictionary<DataComponentType<object>, object>();
        foreach (var key in map.KeySet)
            if (key is DataComponentType<object> type && !type.IsTransient && map.Get(type) is { } value)
                result[type] = value;
        return result;
    }
}

//EmptyDataComponentMap 空映射单例
internal sealed class EmptyDataComponentMap : DataComponentMap
{
    public static readonly EmptyDataComponentMap Instance = new();
    private EmptyDataComponentMap() { }

    public T? Get<T>(DataComponentType<T> type) where T : class => null;
    public IEnumerable<object> KeySet => Array.Empty<object>();
}

//CompositeDataComponentMap 组合映射 overrides 优先 prototype 兜底
internal sealed class CompositeDataComponentMap : DataComponentMap
{
    private readonly DataComponentMap _prototype;
    private readonly DataComponentMap _overrides;

    public CompositeDataComponentMap(DataComponentMap prototype, DataComponentMap overrides)
    {
        _prototype = prototype;
        _overrides = overrides;
    }

    public T? Get<T>(DataComponentType<T> type) where T : class
        => _overrides.Get(type) ?? _prototype.Get(type);

    public IEnumerable<object> KeySet
        => _prototype.KeySet.Concat(_overrides.KeySet).Distinct();
}

//DataComponentLookup 只读查找接口对应原版 net.minecraft.core.component.DataComponentLookup
//DataComponentMap 继承提供 Composite/Builder 等可变操作 DataComponentLookup 只暴露 Get/Has/KeySet
public interface DataComponentLookup : DataComponentGetter
{
    //Empty 空查找单例
    public static DataComponentLookup Empty { get; } = EmptyDataComponentLookup.Instance;

    //Get 按 type 取值不存在返回 null
    T? Get<T>(DataComponentType<T> type) where T : class;

    //KeySet 所有 type 集合用 object 装箱避免 C# 泛型不变性
    IEnumerable<object> KeySet { get; }

    //Has 判 type 是否存在
    bool Has<T>(DataComponentType<T> type) where T : class => Get(type) is not null;

    //Size 组件数量
    int Size => KeySet.Count();

    //IsEmpty 是否为空
    bool IsEmpty => Size == 0;
}

//EmptyDataComponentLookup 空查找单例
internal sealed class EmptyDataComponentLookup : DataComponentLookup
{
    public static readonly EmptyDataComponentLookup Instance = new();
    private EmptyDataComponentLookup() { }

    public T? Get<T>(DataComponentType<T> type) where T : class => null;
    public IEnumerable<object> KeySet => Array.Empty<object>();
}
