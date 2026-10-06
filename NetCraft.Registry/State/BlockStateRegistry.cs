using System.Text;

namespace NetCraft.Registry.State;

//BlockState data storage and lookup, corresponds to vanilla optimization 2.5
//Property data and neighbors for all BlockState instances are stored centrally here, avoiding per-instance arrays
//Aligns with the FerriteCore FastMap idea: the BlockState struct holds only an int Id and looks up data in this table
public static class BlockStateRegistry
{
    private static readonly List<BlockStateData> _all = new();
    private static readonly Dictionary<int, BlockState> _statesById = new();

    //Register a new BlockState and return the struct wrapper
    public static BlockState Register(Block owner, PropertyBase[] keys, object?[] values)
    {
        if (keys.Length != values.Length)
            throw new ArgumentException("propertyKeys/propertyValues length mismatch");
        var id = _all.Count;
        _all.Add(new BlockStateData(owner, keys, values));
        var state = new BlockState(id);
        _statesById[id] = state;
        return state;
    }

    //Inject the precomputed neighbors table using int indexes to avoid BlockState references
    public static void InitializeNeighbors(int stateId, int[][] neighbors)
    {
        if (_all[stateId].Neighbors is not null)
            throw new InvalidOperationException("Neighbors already initialized");
        _all[stateId].Neighbors = neighbors;
    }

    public static int Count => _all.Count;

    //GetState returns a BlockState by id, or default when out of range, for network palette reverse lookup
    public static BlockState GetState(int id)
        => id >= 0 && id < _all.Count ? _statesById[id] : default;

    public static Block Owner(int id) => _all[id].Owner;

    public static IReadOnlyCollection<PropertyBase> GetProperties(int id) => _all[id].PropertyKeys;

    public static bool IsSingletonState(int id) => _all[id].PropertyKeys.Length == 0;

    //Property index lookup; linear search aligns with vanilla ValueIndex
    //Equality is by name and value type rather than reference; the property instance built per block by the block table is not the same as the constant in BlockStateProperties
    //Property names are unique within a block, so matching by name cannot cross over to another property
    private static int ValueIndex(int id, PropertyBase property)
    {
        var keys = _all[id].PropertyKeys;
        for (var i = 0; i < keys.Length; i++)
            if (keys[i].Equals(property)) return i;
        return -1;
    }

    public static bool HasProperty(int id, PropertyBase property) => ValueIndex(id, property) != -1;

    public static T GetValue<T>(int id, Property<T> property) where T : IComparable
    {
        var index = ValueIndex(id, property);
        if (index == -1)
            throw new ArgumentException($"Cannot get property {property} as it does not exist in {Owner(id)}");
        return (T)_all[id].PropertyValues[index]!;
    }

    public static T? GetOptionalValue<T>(int id, Property<T> property) where T : IComparable
    {
        var index = ValueIndex(id, property);
        return index == -1 ? default : (T)_all[id].PropertyValues[index]!;
    }

    public static T GetValueOrElse<T>(int id, Property<T> property, T defaultValue) where T : IComparable
    {
        var index = ValueIndex(id, property);
        return index == -1 ? defaultValue : (T)_all[id].PropertyValues[index]!;
    }

    public static BlockState SetValue<T>(int id, Property<T> property, T value) where T : IComparable
    {
        var index = ValueIndex(id, property);
        if (index == -1)
            throw new ArgumentException($"Cannot set property {property} as it does not exist in {Owner(id)}");
        //The value index must be computed from the block's own property; two instances with the same name and type may have different value ranges
        //Four-way facing and six-way facing share a name and type, so using the wrong instance would compute an out-of-range index
        var actual = _all[id].PropertyKeys[index] as Property<T> ?? property;
        var valueIndex = actual.GetInternalIndex(value);
        if (valueIndex < 0)
            throw new ArgumentException($"Cannot set property {property} to {value} on {Owner(id)}, not an allowed value");
        return _statesById[_all[id].Neighbors![index][valueIndex]];
    }

    public static BlockState TrySetValue<T>(int id, Property<T> property, T value) where T : IComparable
    {
        var index = ValueIndex(id, property);
        if (index == -1) return _statesById[id];
        var actual = _all[id].PropertyKeys[index] as Property<T> ?? property;
        var valueIndex = actual.GetInternalIndex(value);
        if (valueIndex < 0) return _statesById[id];
        return _statesById[_all[id].Neighbors![index][valueIndex]];
    }

    //Non-generic SetValue used by Codec deserialization, not requiring T to be IComparable
    public static BlockState SetValue(int id, PropertyBase property, object value)
    {
        var index = ValueIndex(id, property);
        if (index == -1) return _statesById[id];
        var valueIndex = _all[id].PropertyKeys[index].GetInternalIndexForValue(value);
        if (valueIndex < 0) return _statesById[id];
        return _statesById[_all[id].Neighbors![index][valueIndex]];
    }

    public static BlockState Cycle<T>(int id, Property<T> property) where T : IComparable
    {
        var index = ValueIndex(id, property);
        if (index == -1) return _statesById[id];
        var actual = _all[id].PropertyKeys[index] as Property<T> ?? property;
        var current = (T)_all[id].PropertyValues[index]!;
        return SetValue(id, actual, FindNextInCollection(actual.PossibleValues, current));
    }

    private static T FindNextInCollection<T>(IReadOnlyList<T> list, T t)
    {
        var count = list.Count;
        for (var i = 0; i < count; i++)
            if (EqualityComparer<T>.Default.Equals(list[i], t))
                return i + 1 == count ? list[0] : list[i + 1];
        return list[0];
    }

    public static IEnumerable<PropertyValue> GetValues(int id)
    {
        var data = _all[id];
        for (var i = 0; i < data.PropertyKeys.Length; i++)
            yield return new PropertyValue(data.PropertyKeys[i], data.PropertyValues[i]!);
    }

    public static string ToString(int id)
    {
        var data = _all[id];
        var builder = new StringBuilder();
        builder.Append(data.Owner);
        if (data.PropertyKeys.Length > 0)
        {
            builder.Append('[');
            builder.Append(string.Join(",", GetValues(id).Select(v => v.ToString())));
            builder.Append(']');
        }
        return builder.ToString();
    }

    //Reset clears all registered data, for tests only
    public static void Reset()
    {
        _all.Clear();
        _statesById.Clear();
    }

    private sealed class BlockStateData
    {
        public Block Owner { get; }
        public PropertyBase[] PropertyKeys { get; }
        public object?[] PropertyValues { get; }
        public int[][]? Neighbors { get; set; }

        public BlockStateData(Block owner, PropertyBase[] keys, object?[] values)
        {
            Owner = owner;
            PropertyKeys = keys;
            PropertyValues = values;
        }
    }
}
