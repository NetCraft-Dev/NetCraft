using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using NetCraft.Codec;

namespace NetCraft.Registry.State;

//状态属性非泛型基类，对应原版 Property<?> 用法
//Property<T> 提供类型安全 API，PropertyBase 提供跨 T 的统一接口
public abstract class PropertyBase
{
    private readonly Type _valueClass;
    private readonly string _name;
    private int? _hashCode;

    protected PropertyBase(string name, Type valueClass)
    {
        _name = name;
        _valueClass = valueClass;
    }

    public string Name => _name;
    public Type ValueClass => _valueClass;

    //所有合法取值（装箱形式）
    public abstract IReadOnlyList<object> PossibleValuesAsObjects { get; }

    //按值取字符串名
    public abstract string GetNameForValue(object value);

    //按字符串名取值
    public abstract object? GetValueForName(string name);

    //值在 PossibleValues 中的索引
    public abstract int GetInternalIndexForValue(object value);

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj)) return true;
        if (obj is not PropertyBase that) return false;
        return _valueClass == that._valueClass && _name == that._name;
    }

    public override int GetHashCode()
    {
        _hashCode ??= (31 * _valueClass.GetHashCode()) + _name.GetHashCode();
        return _hashCode.Value;
    }

    public override string ToString() => $"{_name}({_valueClass.Name})";
}

//状态属性泛型抽象，对应原版 Property<T extends Comparable<T>>
//C# enum 默认实现 IComparable 不实现 IComparable<T> 故约束用非泛型 IComparable 兼容 enum
public abstract class Property<T> : PropertyBase where T : IComparable
{
    protected Property(string name) : base(name, typeof(T)) { }

    //所有合法取值（强类型）
    public abstract IReadOnlyList<T> PossibleValues { get; }

    public override IReadOnlyList<object> PossibleValuesAsObjects
        => PossibleValues.Select(v => (object)v).ToList();

    public abstract string GetName(T value);
    public abstract bool TryGetValue(string name, [MaybeNullWhen(false)] out T value);
    public abstract int GetInternalIndex(T value);

    public override string GetNameForValue(object value) => GetName((T)value);
    public override object? GetValueForName(string name) => TryGetValue(name, out var v) ? v : null;
    public override int GetInternalIndexForValue(object value) => GetInternalIndex((T)value);

    //构造值绑定
    public PropertyValue Value(T value) => new(this, value);

    //ValueCodec 属性值的字符串 codec 对应原版 Property.valueCodec
    //encode 用 GetName 把值转字符串 decode 用 TryGetValue 把字符串转值
    public Codec<T> ValueCodec()
        => Codecs.String.ComapFlatMap(
            name => TryGetValue(name, out var v)
                ? DataResult<T>.Success(v)
                : DataResult<T>.Error(() => $"Unknown value '{name}' for property {Name}"),
            value => GetName(value));
}
