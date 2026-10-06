using NetCraft.Codec;

namespace NetCraft.Registry;

//DataComponentMap data component map, maps to vanilla net.minecraft.core.component.DataComponentMap
//Extends the read-only DataComponentLookup interface and adds Composite plus static construction; Builder builds the mutable version in the Network sub-library
public interface DataComponentMap : DataComponentLookup
{
    //Empty empty map singleton
    public static DataComponentMap Empty { get; } = EmptyDataComponentMap.Instance;

    //CODEC whole-map persistence codec; transient components are not written out, maps to vanilla DataComponentMap.CODEC
    public static readonly Codec<DataComponentMap> CODEC = DataComponentType<object>.VALUE_MAP_CODEC.ComapFlatMap(
        map =>
        {
            var builder = new DataComponentMapBuilder();
            foreach (var kv in map) builder.SetUnchecked(kv.Key, kv.Value);
            return DataResult<DataComponentMap>.Success(builder.Build());
        },
        ToValueMap);

    //Composite combines prototype and overrides, overrides take priority
    static DataComponentMap Composite(DataComponentMap prototype, DataComponentMap overrides)
        => new CompositeDataComponentMap(prototype, overrides);

    //Builder creates a component map builder, maps to vanilla builder
    static DataComponentMapBuilder Builder() => new();

    //ToValueMap takes the persistable component entries, filtering transient on the encode side, maps to the encode side of vanilla makeCodecFromMap
    private static Dictionary<DataComponentType<object>, object> ToValueMap(DataComponentMap map)
    {
        var result = new Dictionary<DataComponentType<object>, object>();
        foreach (var key in map.KeySet)
            if (key is DataComponentType<object> type && !type.IsTransient && map.Get(type) is { } value)
                result[type] = value;
        return result;
    }
}

//EmptyDataComponentMap empty map singleton
internal sealed class EmptyDataComponentMap : DataComponentMap
{
    public static readonly EmptyDataComponentMap Instance = new();
    private EmptyDataComponentMap() { }

    public T? Get<T>(DataComponentType<T> type) where T : class => null;
    public IEnumerable<object> KeySet => Array.Empty<object>();
}

//CompositeDataComponentMap composite map, overrides take priority and prototype is the fallback
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

//DataComponentLookup read-only lookup interface, maps to vanilla net.minecraft.core.component.DataComponentLookup
//DataComponentMap extends it with mutable operations like Composite/Builder; DataComponentLookup exposes only Get/Has/KeySet
public interface DataComponentLookup : DataComponentGetter
{
    //Empty empty lookup singleton
    public static DataComponentLookup Empty { get; } = EmptyDataComponentLookup.Instance;

    //Get returns the value by type; returns null if absent
    T? Get<T>(DataComponentType<T> type) where T : class;

    //KeySet all types, boxed as object to avoid C# generic invariance
    IEnumerable<object> KeySet { get; }

    //Has checks whether the type is present
    bool Has<T>(DataComponentType<T> type) where T : class => Get(type) is not null;

    //Size component count
    int Size => KeySet.Count();

    //IsEmpty whether it is empty
    bool IsEmpty => Size == 0;
}

//EmptyDataComponentLookup empty lookup singleton
internal sealed class EmptyDataComponentLookup : DataComponentLookup
{
    public static readonly EmptyDataComponentLookup Instance = new();
    private EmptyDataComponentLookup() { }

    public T? Get<T>(DataComponentType<T> type) where T : class => null;
    public IEnumerable<object> KeySet => Array.Empty<object>();
}
