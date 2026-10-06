namespace NetCraft.Registry.State;

//Boolean property, maps to vanilla BooleanProperty
//Possible value order must match vanilla: true first and false second; the order determines state id assignment
public sealed class BooleanProperty : Property<bool>
{
    private static readonly IReadOnlyList<bool> PossibleValuesBool = new[] { true, false };
    private static readonly IReadOnlyList<object> PossibleValuesObj = new object[] { true, false };

    public BooleanProperty(string name) : base(name) { }

    public override IReadOnlyList<bool> PossibleValues => PossibleValuesBool;

    public override IReadOnlyList<object> PossibleValuesAsObjects => PossibleValuesObj;

    public override string GetName(bool value) => value ? "true" : "false";

    public override bool TryGetValue(string name, out bool value)
    {
        switch (name)
        {
            case "true": value = true; return true;
            case "false": value = false; return true;
            default: value = false; return false;
        }
    }

    public override int GetInternalIndex(bool value) => value ? 0 : 1;
}

//Integer property, maps to vanilla IntegerProperty
//Range is [min, min+count)
public sealed class IntegerProperty : Property<int>
{
    private readonly int _min;
    private readonly int _count;
    private readonly IReadOnlyList<int> _values;
    private readonly IReadOnlyList<object> _valuesObj;

    public IntegerProperty(string name, int min, int maxInclusive) : base(name)
    {
        _min = min;
        _count = maxInclusive - min + 1;
        if (_count < 2)
            throw new ArgumentException($"IntegerProperty {name} needs at least 2 values");
        var arr = new int[_count];
        for (var i = 0; i < _count; i++) arr[i] = min + i;
        _values = arr;
        _valuesObj = arr.Select(v => (object)v).ToArray();
    }

    public override IReadOnlyList<int> PossibleValues => _values;

    public override IReadOnlyList<object> PossibleValuesAsObjects => _valuesObj;

    public override string GetName(int value) => value.ToString();

    public override bool TryGetValue(string name, out int value)
    {
        if (int.TryParse(name, out value) && value >= _min && value < _min + _count)
            return true;
        value = default;
        return false;
    }

    public override int GetInternalIndex(int value) => value >= _min && value < _min + _count ? value - _min : -1;
}

//Enum property, maps to vanilla EnumProperty
//Member order is the state id assignment order; vanilla takes the enum declaration order and the generated enums list values in declaration order
//Supports taking a subset of the enum; vanilla FACING_HOPPER using only directions other than UP is such a case
public sealed class EnumProperty<T> : Property<T> where T : struct, Enum
{
    private readonly IReadOnlyList<T> _values;
    private readonly Dictionary<string, T> _byName;

    public EnumProperty(string name) : this(name, Enum.GetValues<T>()) { }

    public EnumProperty(string name, IReadOnlyList<T> values) : base(name)
    {
        _values = values;
        _byName = new(values.Count, StringComparer.Ordinal);
        foreach (var v in values)
            _byName[GetName(v)] = v;
    }

    public override IReadOnlyList<T> PossibleValues => _values;

    public override string GetName(T value) => value.ToString();

    public override bool TryGetValue(string name, out T value)
        => _byName.TryGetValue(name, out value);

    public override int GetInternalIndex(T value)
    {
        for (var i = 0; i < _values.Count; i++)
            if (EqualityComparer<T>.Default.Equals(_values[i], value)) return i;
        return -1;
    }
}
