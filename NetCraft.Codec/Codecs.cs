namespace NetCraft.Codec;

//Collection of basic scalar codecs, mirroring vanilla Codec's static factories
//Provides scalar codecs for byte/short/int/long/float/double/bool/string
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

    //WithAlternative tries first and uses second on failure, mirroring vanilla Codec.withAlternative
    public static Codec<T> WithAlternative<T>(Codec<T> first, Codec<T> second)
        => new AlternativeCodec<T>(first, second);

    //Either parses with first and falls back to second, mirroring vanilla Codec.either
    public static Codec<Alt<A, B>> Either<A, B>(Codec<A> first, Codec<B> second)
        => new EitherCodec<A, B>(first, second);

    //DispatchedMap is a map where the key decides the value codec, mirroring vanilla Codec.dispatchedMap
    //keyCodec decodes the key and valueCodecGetter supplies the value codec for that key
    public static Codec<Dictionary<K, V>> DispatchedMap<K, V>(Codec<K> keyCodec, Func<K, Codec<V>> valueCodecGetter)
        where K : notnull
        => new DispatchedMapCodec<K, V>(keyCodec, valueCodecGetter);

    //UnboundedMap is a map whose keys and values are encoded independently, mirroring vanilla Codec.unboundedMap
    public static Codec<Dictionary<K, V>> UnboundedMap<K, V>(Codec<K> keyCodec, Codec<V> valueCodec)
        where K : notnull
        => new UnboundedMapCodec<K, V>(keyCodec, valueCodec);
}

//UnboundedMapCodec is the map implementation with independent key and value codecs
internal sealed class UnboundedMapCodec<K, V> : ScalarCodec<Dictionary<K, V>> where K : notnull
{
    private readonly Codec<K> _keyCodec;
    private readonly Codec<V> _valueCodec;

    public UnboundedMapCodec(Codec<K> keyCodec, Codec<V> valueCodec)
    {
        _keyCodec = keyCodec;
        _valueCodec = valueCodec;
    }

    public override DataResult<Dictionary<K, V>> Parse<U>(DynamicOps<U> ops, U input)
    {
        return ops.GetMapValues(input).FlatMap(entries =>
        {
            var map = new Dictionary<K, V>();
            foreach (var entry in entries)
            {
                var keyResult = _keyCodec.Parse(ops, entry.First);
                if (!keyResult.Result().IsPresent)
                    return DataResult<Dictionary<K, V>>.Error(() => "Map key failed to parse");
                var valueResult = _valueCodec.Parse(ops, entry.Second);
                if (!valueResult.Result().IsPresent)
                    return DataResult<Dictionary<K, V>>.Error(() => "Map value failed to parse");
                map[keyResult.GetOrThrow()] = valueResult.GetOrThrow();
            }
            return DataResult<Dictionary<K, V>>.Success(map);
        });
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Dictionary<K, V> value)
    {
        var pairs = new List<Pair<U, U>>(value.Count);
        foreach (var kv in value)
        {
            var keyResult = _keyCodec.EncodeStart(ops, kv.Key);
            if (!keyResult.Result().IsPresent)
                return DataResult<U>.Error(() => "Map key failed to encode");
            var valueResult = _valueCodec.EncodeStart(ops, kv.Value);
            if (!valueResult.Result().IsPresent)
                return DataResult<U>.Error(() => "Map value failed to encode");
            pairs.Add(new Pair<U, U>(keyResult.GetOrThrow(), valueResult.GetOrThrow()));
        }
        return DataResult<U>.Success(ops.CreateMap(pairs));
    }
}

//DispatchedMapCodec is the key-dispatched map codec implementation
//Decoding walks the map entry by entry, decoding the key first and then the value with that key's codec; encoding is the reverse
internal sealed class DispatchedMapCodec<K, V> : ScalarCodec<Dictionary<K, V>> where K : notnull
{
    private readonly Codec<K> _keyCodec;
    private readonly Func<K, Codec<V>> _valueCodecGetter;

    public DispatchedMapCodec(Codec<K> keyCodec, Func<K, Codec<V>> valueCodecGetter)
    {
        _keyCodec = keyCodec;
        _valueCodecGetter = valueCodecGetter;
    }

    public override DataResult<Dictionary<K, V>> Parse<U>(DynamicOps<U> ops, U input)
    {
        return ops.GetMapValues(input).FlatMap(entries =>
        {
            var map = new Dictionary<K, V>();
            foreach (var entry in entries)
            {
                var keyResult = _keyCodec.Parse(ops, entry.First);
                if (!keyResult.Result().IsPresent)
                    return DataResult<Dictionary<K, V>>.Error(() => "Map key failed to parse");
                var key = keyResult.GetOrThrow();
                var valueResult = _valueCodecGetter(key).Parse(ops, entry.Second);
                if (!valueResult.Result().IsPresent)
                    return DataResult<Dictionary<K, V>>.Error(() => $"Map value failed to parse: {key}");
                map[key] = valueResult.GetOrThrow();
            }
            return DataResult<Dictionary<K, V>>.Success(map);
        });
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Dictionary<K, V> value)
    {
        var pairs = new List<Pair<U, U>>(value.Count);
        foreach (var kv in value)
        {
            var keyResult = _keyCodec.EncodeStart(ops, kv.Key);
            if (!keyResult.Result().IsPresent)
                return DataResult<U>.Error(() => $"Map key failed to encode: {kv.Key}");
            var valueResult = _valueCodecGetter(kv.Key).EncodeStart(ops, kv.Value);
            if (!valueResult.Result().IsPresent)
                return DataResult<U>.Error(() => $"Map value failed to encode: {kv.Key}");
            pairs.Add(new Pair<U, U>(keyResult.GetOrThrow(), valueResult.GetOrThrow()));
        }
        return DataResult<U>.Success(ops.CreateMap(pairs));
    }
}

//Alternative codec, mirroring vanilla Codec.AlternativeCodec
//parse tries first and returns on success, otherwise tries second
//encode tries first and returns on success, otherwise tries second
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
