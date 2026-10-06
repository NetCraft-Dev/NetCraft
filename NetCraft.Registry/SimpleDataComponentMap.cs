namespace NetCraft.Registry;

//SimpleDataComponentMap simple DataComponentMap implementation, maps to vanilla DataComponentMap.Builder.ImmutableMap
//Backed by Dictionary<object,object>; the key is the DataComponentType instance and the value is a boxed value
//Get looks up by type reference and casts to T; the caller guarantees the type is the same instance used at insertion
public sealed class SimpleDataComponentMap : DataComponentMap
{
    private readonly Dictionary<object, object> _map;

    public SimpleDataComponentMap(Dictionary<object, object> map)
    {
        _map = map;
    }

    public T? Get<T>(DataComponentType<T> type) where T : class
        => _map.TryGetValue(type, out var value) ? (T)value : null;

    public IEnumerable<object> KeySet => _map.Keys;
}

//DataComponentMapBuilder DataComponentMap builder, maps to vanilla DataComponentMap.Builder
//add by TypedDataComponent, set by type+value, remove by type
//build returns an immutable SimpleDataComponentMap
public sealed class DataComponentMapBuilder
{
    private readonly Dictionary<object, object> _map = new();

    //Add by TypedDataComponent
    public DataComponentMapBuilder Add<T>(TypedDataComponent<T> component) where T : class
    {
        _map[component.Type] = component.Value;
        return this;
    }

    //Set by type+value
    public DataComponentMapBuilder Set<T>(DataComponentType<T> type, T value) where T : class
    {
        _map[type] = value;
        return this;
    }

    //Remove by type
    public DataComponentMapBuilder Remove<T>(DataComponentType<T> type) where T : class
    {
        _map.Remove(type);
        return this;
    }

    //SetUnchecked writes by non-generic key/value for cases that only know object, such as patch splitting and whole-map decoding, maps to vanilla setUnchecked
    public DataComponentMapBuilder SetUnchecked(object type, object value)
    {
        _map[type] = value;
        return this;
    }

    //Build returns a SimpleDataComponentMap
    public DataComponentMap Build()
        => new SimpleDataComponentMap(new Dictionary<object, object>(_map));

    //BuildFromMapTrusted builds from an existing Dictionary, trusting the input without copying
    public static DataComponentMap BuildFromMapTrusted(Dictionary<object, object> map)
        => new SimpleDataComponentMap(map);
}
