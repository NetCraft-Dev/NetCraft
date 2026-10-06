namespace NetCraft.Registry.State;

//Property value binding, maps to vanilla Property.Value<T>
//Non-generic, because Property<T>.Value returns a PropertyValue
public sealed record PropertyValue(PropertyBase Property, object Value)
{
    public override string ToString() => $"{Property.Name}={Property.GetNameForValue(Value)}";

    public string ValueName => Property.GetNameForValue(Value);
}
