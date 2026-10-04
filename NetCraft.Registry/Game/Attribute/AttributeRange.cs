using NetCraft.Codec;
using NetCraft.Util;

namespace NetCraft.Registry.Environment;

//AttributeRange 属性值合法区间对应原版 AttributeRange
public interface AttributeRange<Value>
{
    //Validate 校验值是否落在区间内
    DataResult<Value> Validate(Value value);

    //Sanitize 把值夹到区间内
    Value Sanitize(Value value);

    //Any 不限制取值
    static AttributeRange<Value> Any() => AnyRange<Value>.Instance;

    //OfFloat 闭区间 [minValue, maxValue]
    static AttributeRange<float> OfFloat(float minValue, float maxValue) => new FloatRange(minValue, maxValue);

    //UnitFloat 单位区间 [0,1]
    static AttributeRange<float> UnitFloat => FloatRange.Unit;

    //NonNegativeFloat 非负区间 [0,+inf]
    static AttributeRange<float> NonNegativeFloat => FloatRange.NonNegative;
}

//AnyRange 任意值都合法
internal sealed class AnyRange<Value> : AttributeRange<Value>
{
    public static readonly AnyRange<Value> Instance = new();

    public DataResult<Value> Validate(Value value) => DataResult<Value>.Success(value);

    public Value Sanitize(Value value) => value;
}

//FloatRange 浮点闭区间
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
