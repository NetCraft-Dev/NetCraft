using NetCraft.Commands.Context;

namespace NetCraft.Commands.Suggestion;

//IntegerSuggestion 整数建议项对应原版com.mojang.brigadier.suggestion.IntegerSuggestion
//包装整数值并继承Suggestion用于数值补全
public sealed class IntegerSuggestion : Suggestion, IEquatable<IntegerSuggestion>
{
    private readonly int _value;

    public IntegerSuggestion(StringRange range, int value) : this(range, value, null)
    {
    }

    public IntegerSuggestion(StringRange range, int value, IMessage? tooltip)
        : base(range, value.ToString(), tooltip)
    {
        _value = value;
    }

    public int Value => _value;

    public bool Equals(IntegerSuggestion? other)
    {
        if (other is null)
        {
            return false;
        }
        if (ReferenceEquals(this, other))
        {
            return true;
        }
        return _value == other._value && base.Equals(other);
    }

    public override bool Equals(object? obj) => obj is IntegerSuggestion other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return (base.GetHashCode() * 31) ^ _value;
        }
    }

    //CompareTo 同为IntegerSuggestion按数值比较否则按文本
    public override int CompareTo(Suggestion? o)
    {
        if (o is IntegerSuggestion integerSuggestion)
        {
            return _value.CompareTo(integerSuggestion._value);
        }
        return base.CompareTo(o);
    }

    public override int CompareToIgnoreCase(Suggestion b) => CompareTo(b);

    public override string ToString()
    {
        return "IntegerSuggestion{value=" + _value + ", range=" + Range + ", text='" + Text + "', tooltip='" + Tooltip + "'}";
    }
}
