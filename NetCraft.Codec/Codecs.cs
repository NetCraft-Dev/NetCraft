namespace NetCraft.Codec;

//基础标量codec集合对应原版Codec的静态工厂
//提供byte/short/int/long/float/double/bool/string的标量编解码
public static class Codecs
{
    public static readonly Codec<byte> Byte = new ByteCodec();

    public static readonly Codec<short> Short = new ShortCodec();

    public static readonly Codec<int> Int = new IntCodec();

    public static readonly Codec<long> Long = new LongCodec();

    public static readonly Codec<float> Float = new FloatCodec();

    public static readonly Codec<double> Double = new DoubleCodec();

    public static readonly Codec<bool> Bool = new BoolCodec();

    public static readonly Codec<string> String = new StringCodec();

    //WithAlternative先尝试first失败用second对应原版Codec.withAlternative
    public static Codec<T> WithAlternative<T>(Codec<T> first, Codec<T> second)
        => new AlternativeCodec<T>(first, second);
}

//Alternative codec对应原版Codec.AlternativeCodec
//parse先试first成功返回失败试second
//encode先试first成功返回失败试second
internal sealed class AlternativeCodec<T> : ScalarCodec<T>
{
    private readonly Codec<T> _first;
    private readonly Codec<T> _second;

    public AlternativeCodec(Codec<T> first, Codec<T> second)
    {
        _first = first;
        _second = second;
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, T value)
    {
        var firstResult = _first.EncodeStart(ops, value);
        if (firstResult.Result().IsPresent) return firstResult;
        return _second.EncodeStart(ops, value);
    }

    public override DataResult<T> Parse<U>(DynamicOps<U> ops, U input)
    {
        var firstResult = _first.Parse(ops, input);
        if (firstResult.Result().IsPresent) return firstResult;
        return _second.Parse(ops, input);
    }
}

internal sealed class ByteCodec : ScalarCodec<byte>
{
    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, byte value)
        => DataResult<U>.Success(ops.CreateByte(value));

    public override DataResult<byte> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetNumberValue(input).Map(v => (byte)v);
}

internal sealed class ShortCodec : ScalarCodec<short>
{
    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, short value)
        => DataResult<U>.Success(ops.CreateShort(value));

    public override DataResult<short> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetNumberValue(input).Map(v => (short)v);
}

internal sealed class IntCodec : ScalarCodec<int>
{
    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, int value)
        => DataResult<U>.Success(ops.CreateInt(value));

    public override DataResult<int> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetNumberValue(input).Map(v => (int)v);
}

internal sealed class LongCodec : ScalarCodec<long>
{
    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, long value)
        => DataResult<U>.Success(ops.CreateLong(value));

    public override DataResult<long> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetLongValue(input);
}

internal sealed class FloatCodec : ScalarCodec<float>
{
    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, float value)
        => DataResult<U>.Success(ops.CreateFloat(value));

    public override DataResult<float> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetNumberValue(input).Map(v => (float)v);
}

internal sealed class DoubleCodec : ScalarCodec<double>
{
    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, double value)
        => DataResult<U>.Success(ops.CreateDouble(value));

    public override DataResult<double> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetNumberValue(input);
}

internal sealed class BoolCodec : ScalarCodec<bool>
{
    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, bool value)
        => DataResult<U>.Success(ops.CreateBoolean(value));

    public override DataResult<bool> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetBooleanValue(input);
}

internal sealed class StringCodec : ScalarCodec<string>
{
    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, string value)
        => DataResult<U>.Success(ops.CreateString(value));

    public override DataResult<string> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetStringValue(input);
}
