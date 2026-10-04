using System.Text;

namespace NetCraft.Registry.State;

//状态持有者，对应原版 StateHolder<O, S>
//O 是所有者类型如 Block，S 是自身类型如 BlockState
//核心机制 neighbors 二维数组预计算所有相邻状态 setValue O(1)
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

    //循环切换属性到下一个值
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

    //设置属性值，返回邻居状态，找不到属性抛异常
    public S SetValue<T>(Property<T> property, T value) where T : IComparable
    {
        var index = ValueIndex(property);
        if (index == ValueNotFound)
            throw new ArgumentException($"Cannot set property {property} as it does not exist in {Owner}");
        return SetValueInternal(property, index, value!);
    }

    //尝试设置，找不到属性返回当前状态
    public S TrySetValue<T>(Property<T> property, T value) where T : IComparable
    {
        var index = ValueIndex(property);
        if (index == ValueNotFound) return (S)this;
        return SetValueInternal(property, index, value!);
    }

    //非泛型SetValue用于PropertiesCodec反序列化不要求T为IComparable
    //找不到属性或值非法返回当前状态不抛
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

    //由 StateDefinition 调用注入预计算的邻居表
    public void InitializeNeighbors(S?[][] neighbors)
    {
        if (_neighbors != null)
            throw new InvalidOperationException("Neighbors already initialized");
        _neighbors = neighbors;
    }

    //无属性的单一状态
    public bool IsSingletonState => _propertyKeys.Length == 0;

    //所有属性的当前值
    public IEnumerable<PropertyValue> GetValues()
    {
        for (var i = 0; i < _propertyKeys.Length; i++)
            yield return new PropertyValue(_propertyKeys[i], _propertyValues[i]!);
    }
}
