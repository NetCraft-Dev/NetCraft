using NetCraft.Codec;
using NetCraft.Util;

namespace NetCraft.Registry.Environment;

//AttributeRange valid range for attribute values, maps to vanilla AttributeRange
public interface AttributeRange<Value>
{
    //Validate checks whether the value falls within the range
    DataResult<Value> Validate(Value value);

    //Sanitize clamps the value into the range
    Value Sanitize(Value value);

    //Any does not restrict the value
    static AttributeRange<Value> Any() => AnyRange<Value>.Instance;

    //OfFloat closed range [minValue, maxValue]
    static AttributeRange<float> OfFloat(float minValue, float maxValue) => new FloatRange(minValue, maxValue);

    //UnitFloat unit range [0,1]
    static AttributeRange<float> UnitFloat => FloatRange.Unit;

    //NonNegativeFloat non-negative range [0,+inf]
    static AttributeRange<float> NonNegativeFloat => FloatRange.NonNegative;
}

//AnyRange any value is valid
internal sealed class AnyRange<Value> : AttributeRange<Value>
{
    public static readonly AnyRange<Value> Instance = new();

    public DataResult<Value> Validate(Value value) => DataResult<Value>.Success(value);

    public Value Sanitize(Value value) => value;
}

//FloatRange closed float range
internal sealed class FloatRange : AttributeRange<float>
{
    public static readonly FloatRange Unit = new(0.0f, 1.0f);
    public static readonly FloatRange NonNegative = new(0.0f, float.PositiveInfinity);

    private readonly float _minValue;
    private readonly float _maxValue;

    public FloatRange(float minValue, float maxValue)
    {
        _minValue = minValue;
        _maxValue = maxValue;
    }

    public DataResult<float> Validate(float value)
        => value >= _minValue && value <= _maxValue
            ? DataResult<float>.Success(value)
            : DataResult<float>.Error(() => $"{value} is not in range [{_minValue}; {_maxValue}]");

    public float Sanitize(float value)
        => value >= _minValue && value <= _maxValue ? value : Mth.Clamp(value, _minValue, _maxValue);
}
