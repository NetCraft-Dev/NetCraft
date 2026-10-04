using NetCraft.Codec;
using NetCraft.Registry.Codec;

namespace NetCraft.Registry;

//BiomeCodecs 群系数据模型复用的通用编解码器
internal static class BiomeCodecs
{
    //PositiveInt 正整数对应原版 ExtraCodecs.POSITIVE_INT
    public static readonly Codec<int> PositiveInt = new IntMinCodec(1, "Value must be positive");

    //NonNegativeInt 非负整数对应原版 ExtraCodecs.NON_NEGATIVE_INT
    public static readonly Codec<int> NonNegativeInt = new IntMinCodec(0, "Value must be non-negative");
}

//IntMinCodec 带下界校验的整数
internal sealed class IntMinCodec : ScalarCodec<int>
{
    private readonly int _min;
    private readonly string _message;

    public IntMinCodec(int min, string message)
    {
        _min = min;
        _message = message;
    }

    public override DataResult<int> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetNumberValue(input).FlatMap(value =>
        {
            var result = (int)value;
            return result >= _min
                ? DataResult<int>.Success(result)
                : DataResult<int>.Error(() => $"{_message}: {result}");
        });

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, int value)
        => DataResult<U>.Success(ops.CreateInt(value));
}

//FloatRangeCodec 带上下界校验的浮点对应原版 Codec.floatRange
internal sealed class FloatRangeCodec : ScalarCodec<float>
{
    private readonly float _min;
    private readonly float _max;

    public FloatRangeCodec(float min, float max)
    {
        _min = min;
        _max = max;
    }

    public override DataResult<float> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetNumberValue(input).FlatMap(value =>
        {
            var result = (float)value;
            return result >= _min && result <= _max
                ? DataResult<float>.Success(result)
                : DataResult<float>.Error(() => $"Value must be within range [{_min}; {_max}]: {result}");
        });

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, float value)
        => DataResult<U>.Success(ops.CreateFloat(value));
}

//StringEnumCodec 按序列化名编解码枚举对应原版 StringRepresentable.fromEnum
internal sealed class StringEnumCodec<T> : ScalarCodec<T> where T : struct, Enum
{
    private readonly Dictionary<string, T> _byName;
    private readonly Dictionary<T, string> _names;

    public StringEnumCodec(params (T Value, string Name)[] values)
    {
        _byName = new Dictionary<string, T>(values.Length);
        _names = new Dictionary<T, string>(values.Length);
        foreach (var (value, name) in values)
        {
            _byName[name] = value;
            _names[value] = name;
        }
    }

    public override DataResult<T> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetStringValue(input).FlatMap(name =>
            _byName.TryGetValue(name, out var value)
                ? DataResult<T>.Success(value)
                : DataResult<T>.Error(() => $"Unknown {typeof(T).Name}: {name}"));

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, T value)
        => _names.TryGetValue(value, out var name)
            ? DataResult<U>.Success(ops.CreateString(name))
            : DataResult<U>.Error(() => $"Unregistered {typeof(T).Name}: {value}");
}

//ValidatedCodec 解码后追加校验对应原版 Codec.validate
internal sealed class ValidatedCodec<T> : ScalarCodec<T>
{
    private readonly Codec<T> _inner;
    private readonly Func<T, DataResult<T>> _validator;

    public ValidatedCodec(Codec<T> inner, Func<T, DataResult<T>> validator)
    {
        _inner = inner;
        _validator = validator;
    }

    public override DataResult<T> Parse<U>(DynamicOps<U> ops, U input)
        => _inner.Parse(ops, input).FlatMap(_validator);

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, T value)
        => _inner.EncodeStart(ops, value);
}

//SimpleMapCodec 字符串键映射对应原版 Codec.simpleMap
//键值都走各自的 codec 编解码不额外限定键集合
internal sealed class SimpleMapCodec<K, V> : ScalarCodec<IReadOnlyDictionary<K, V>> where K : notnull
{
    private readonly Codec<K> _keyCodec;
    private readonly Codec<V> _valueCodec;

    public SimpleMapCodec(Codec<K> keyCodec, Codec<V> valueCodec)
    {
        _keyCodec = keyCodec;
        _valueCodec = valueCodec;
    }

    public override DataResult<IReadOnlyDictionary<K, V>> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map =>
        {
            var result = new Dictionary<K, V>();
            foreach (var entry in map.Entries())
            {
                var key = _keyCodec.Parse(ops, entry.First);
                if (!key.Result().IsPresent)
                    return DataResult<IReadOnlyDictionary<K, V>>.Error(() => $"Invalid map key: {entry.First}");
                var value = _valueCodec.Parse(ops, entry.Second);
                if (!value.Result().IsPresent)
                    return DataResult<IReadOnlyDictionary<K, V>>.Error(() => $"Invalid map value for key: {entry.First}");
                result[key.GetOrThrow()] = value.GetOrThrow();
            }
            return DataResult<IReadOnlyDictionary<K, V>>.Success(result);
        });

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, IReadOnlyDictionary<K, V> value)
    {
        var pairs = value
            .Select(entry => new Pair<U, U>(
                _keyCodec.EncodeStart(ops, entry.Key).GetOrThrow(),
                _valueCodec.EncodeStart(ops, entry.Value).GetOrThrow()))
            .ToList();
        return DataResult<U>.Success(ops.CreateMap(pairs));
    }
}

//HolderSetIdCodec carvers 弱引用 codec 单个 id 字符串或 id 数组都接受
//对应原版 ConfiguredWorldCarver.LIST_CODEC 的 HolderSet 两种形态
internal sealed class HolderSetIdCodec : ScalarCodec<IReadOnlyList<Identifier>>
{
    public static readonly HolderSetIdCodec Instance = new();

    public override DataResult<IReadOnlyList<Identifier>> Parse<U>(DynamicOps<U> ops, U input)
    {
        var single = IdentifierCodec.Instance.Parse(ops, input);
        if (single.Result().IsPresent)
            return DataResult<IReadOnlyList<Identifier>>.Success(new[] { single.GetOrThrow() });
        return IdentifierCodec.Instance.ListOf().Parse(ops, input);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, IReadOnlyList<Identifier> value)
        => IdentifierCodec.Instance.ListOf().EncodeStart(ops, value);
}
