using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using NetCraft.Codec;

namespace NetCraft.Registry.State;

//Non-generic base class for state properties, following vanilla Property<?> usage
//Property<T> provides the type-safe API while PropertyBase provides a unified interface across T
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

    //All legal values (boxed form)
    public abstract IReadOnlyList<object> PossibleValuesAsObjects { get; }

    //Get the string name for a value
    public abstract string GetNameForValue(object value);

    //Get the value for a string name
    public abstract object? GetValueForName(string name);

    //Index of the value in PossibleValues
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

//Generic abstract state property, maps to vanilla Property<T extends Comparable<T>>
//C# enums implement IComparable but not IComparable<T>, so the constraint uses the non-generic IComparable to support enums
public abstract class Property<T> : PropertyBase where T : IComparable
{
    protected Property(string name) : base(name, typeof(T)) { }

    //All legal values (strongly typed)
    public abstract IReadOnlyList<T> PossibleValues { get; }

    public override IReadOnlyList<object> PossibleValuesAsObjects
        => PossibleValues.Select(v => (object)v).ToList();

    public abstract string GetName(T value);
    public abstract bool TryGetValue(string name, [MaybeNullWhen(false)] out T value);
    public abstract int GetInternalIndex(T value);

    public override string GetNameForValue(object value) => GetName((T)value);
    public override object? GetValueForName(string name) => TryGetValue(name, out var v) ? v : null;
    public override int GetInternalIndexForValue(object value) => GetInternalIndex((T)value);

    //Construct a value binding
    public PropertyValue Value(T value) => new(this, value);

    //ValueCodec string codec for property values, maps to vanilla Property.valueCodec
    //encode uses GetName to turn a value into a string and decode uses TryGetValue to turn a string back into a value
    public Codec<T> ValueCodec()
        => Codecs.String.ComapFlatMap(
            name => TryGetValue(name, out var v)
                ? DataResult<T>.Success(v)
                : DataResult<T>.Error(() => $"Unknown value '{name}' for property {Name}"),
            value => GetName(value));
}
