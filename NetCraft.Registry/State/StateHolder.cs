using System.Text;

namespace NetCraft.Registry.State;

//State holder, maps to vanilla StateHolder<O, S>
//O is the owner type such as Block and S is the self type such as BlockState
//Core mechanism: the neighbors 2D array precomputes all adjacent states so setValue is O(1)
public abstract class StateHolder<O, S> where S : StateHolder<O, S>
{
    private const int ValueNotFound = -1;
    public const string NameTag = "Name";
    public const string PropertiesTag = "Properties";

    public O Owner { get; }
    private readonly PropertyBase[] _propertyKeys;
    private readonly object?[] _propertyValues;
    private S?[][]? _neighbors;

    protected StateHolder(O owner, PropertyBase[] propertyKeys, object?[] propertyValues)
    {
        if (propertyKeys.Length != propertyValues.Length)
            throw new ArgumentException("propertyKeys/propertyValues length mismatch");
        Owner = owner;
        _propertyKeys = propertyKeys;
        _propertyValues = propertyValues;
    }

    //Cycle the property to its next value
    public S Cycle<T>(Property<T> property) where T : IComparable
        => SetValue(property, FindNextInCollection(property.PossibleValues, GetValue(property)!));

    protected static T FindNextInCollection<T>(IReadOnlyList<T> list, T t)
    {
        var count = list.Count;
        for (var i = 0; i < count; i++)
            if (EqualityComparer<T>.Default.Equals(list[i], t))
                return i + 1 == count ? list[0] : list[i + 1];
        return list[0];
    }

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(Owner);
        if (!IsSingletonState)
        {
            builder.Append('[');
            builder.Append(string.Join(",", GetValues().Select(v => v.ToString())));
            builder.Append(']');
        }
        return builder.ToString();
    }

    public IReadOnlyCollection<PropertyBase> GetProperties() => _propertyKeys;

    private int ValueIndex(PropertyBase property)
    {
        for (var i = 0; i < _propertyKeys.Length; i++)
            if (_propertyKeys[i] == property) return i;
        return ValueNotFound;
    }

    public bool HasProperty(PropertyBase property) => ValueIndex(property) != ValueNotFound;

    private object? GetNullableValue<T>(Property<T> property) where T : IComparable
    {
        var index = ValueIndex(property);
        if (index == ValueNotFound) return null;
        return _propertyValues[index];
    }

    public T GetValue<T>(Property<T> property) where T : IComparable
    {
        var v = GetNullableValue(property);
        if (v is null)
            throw new ArgumentException($"Cannot get property {property} as it does not exist in {Owner}");
        return (T)v;
    }

    public T? GetOptionalValue<T>(Property<T> property) where T : IComparable
    {
        var v = GetNullableValue(property);
        return v is null ? default : (T)v;
    }

    public T GetValueOrElse<T>(Property<T> property, T defaultValue) where T : IComparable
    {
        var v = GetNullableValue(property);
        return v is null ? defaultValue : (T)v;
    }

    //Set a property value, returning the neighbor state; throws if the property is missing
    public S SetValue<T>(Property<T> property, T value) where T : IComparable
    {
        var index = ValueIndex(property);
        if (index == ValueNotFound)
            throw new ArgumentException($"Cannot set property {property} as it does not exist in {Owner}");
        return SetValueInternal(property, index, value!);
    }

    //Try to set; returns the current state if the property is missing
    public S TrySetValue<T>(Property<T> property, T value) where T : IComparable
    {
        var index = ValueIndex(property);
        if (index == ValueNotFound) return (S)this;
        return SetValueInternal(property, index, value!);
    }

    //Non-generic SetValue used by PropertiesCodec deserialization, not requiring T to be IComparable
    //Returns the current state without throwing if the property is missing or the value is invalid
    public S SetValue(PropertyBase property, object value)
    {
        var index = ValueIndex(property);
        if (index == ValueNotFound) return (S)this;
        var valueIndex = property.GetInternalIndexForValue(value);
        if (valueIndex < 0) return (S)this;
        return _neighbors![index][valueIndex]!;
    }

    private S SetValueInternal<T>(Property<T> property, int propertyIndex, object value) where T : IComparable
    {
        var valueIndex = property.GetInternalIndex((T)value);
        if (valueIndex < 0)
            throw new ArgumentException($"Cannot set property {property} to {value} on {Owner}, not an allowed value");
        return _neighbors![propertyIndex][valueIndex]!;
    }

    //Called by StateDefinition to inject the precomputed neighbor table
    public void InitializeNeighbors(S?[][] neighbors)
    {
        if (_neighbors != null)
            throw new InvalidOperationException("Neighbors already initialized");
        _neighbors = neighbors;
    }

    //A singleton state with no properties
    public bool IsSingletonState => _propertyKeys.Length == 0;

    //Current values of all properties
    public IEnumerable<PropertyValue> GetValues()
    {
        for (var i = 0; i < _propertyKeys.Length; i++)
            yield return new PropertyValue(_propertyKeys[i], _propertyValues[i]!);
    }
}
