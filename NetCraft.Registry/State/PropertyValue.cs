namespace NetCraft.Registry.State;

//属性值绑定，对应原版 Property.Value<T>
//非泛型，因为 Property<T>.Value 会返回 PropertyValue
public sealed record PropertyValue(PropertyBase Property, object Value)
{
    public override string ToString() => $"{Property.Name}={Property.GetNameForValue(Value)}";

    public string ValueName => Property.GetNameForValue(Value);
}
