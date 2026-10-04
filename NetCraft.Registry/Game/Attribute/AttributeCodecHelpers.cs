using System.Globalization;
using NetCraft.Codec;
using NetCraft.Registry.Codec;

namespace NetCraft.Registry.Environment;

//ValidatingCodec 给已有 codec 追加解码校验对应原版 Codec.validate
internal sealed class ValidatingCodec<T> : ScalarCodec<T>
{
    private readonly Codec<T> _inner;
    private readonly Func<T, DataResult<T>> _validator;

    public ValidatingCodec(Codec<T> inner, Func<T, DataResult<T>> validator)
    {
        _inner = inner;
        _validator = validator;
    }

    public override DataResult<T> Parse<U>(DynamicOps<U> ops, U input)
        => _inner.Parse(ops, input).FlatMap(_validator);

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, T value)
        => _inner.EncodeStart(ops, value);
}

//ObjectValueCodec 把 Codec<T> 擦除为 Codec<object>，供修饰符库统一 Argument 类型
internal sealed class ObjectValueCodec<T> : ScalarCodec<object>
{
    private readonly Codec<T> _inner;

    public ObjectValueCodec(Codec<T> inner) { _inner = inner; }

    public override DataResult<object> Parse<U>(DynamicOps<U> ops, U input)
        => _inner.Parse(ops, input).Map(v => (object)v!);

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, object value)
        => _inner.EncodeStart(ops, (T)value);
}

//CompactListCodec 单元素与数组都接受对应原版 ExtraCodecs.compactListCodec
internal sealed class CompactListCodec<T> : ScalarCodec<IReadOnlyList<T>>
{
    private readonly Codec<T> _elementCodec;

    public CompactListCodec(Codec<T> elementCodec) { _elementCodec = elementCodec; }

    public override DataResult<IReadOnlyList<T>> Parse<U>(DynamicOps<U> ops, U input)
    {
        var single = _elementCodec.Parse(ops, input);
        if (single.Result().IsPresent)
            return DataResult<IReadOnlyList<T>>.Success(new[] { single.GetOrThrow() });
        return _elementCodec.ListOf().Parse(ops, input);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, IReadOnlyList<T> value)
        => _elementCodec.ListOf().EncodeStart(ops, value);
}

//HexColorCodec 十六进制颜色串对应原版 ExtraCodecs.hexColor
//6 位输出 rgb 形态，8 位输出 argb 形态
internal sealed class HexColorCodec : ScalarCodec<int>
{
    //StringRgb 对应原版 ExtraCodecs.STRING_RGB_COLOR
    public static readonly Codec<int> StringRgb = Codecs.WithAlternative(new HexColorCodec(6), Codecs.Int);

    //StringArgb 对应原版 ExtraCodecs.STRING_ARGB_COLOR
    public static readonly Codec<int> StringArgb = Codecs.WithAlternative(new HexColorCodec(8), Codecs.Int);

    private readonly int _digits;
    private readonly bool _rgb;

    private HexColorCodec(int digits)
    {
        _digits = digits;
        _rgb = digits == 6;
    }

    public override DataResult<int> Parse<U>(DynamicOps<U> ops, U input)
    {
        var stringResult = ops.GetStringValue(input);
        if (!stringResult.Result().IsPresent)
            return DataResult<int>.Error(() => $"Hex color must be a string: {input}");
        var text = stringResult.GetOrThrow();
        if (!text.StartsWith('#'))
            return DataResult<int>.Error(() => "Hex color must begin with #");
        var digits = text.Length - 1;
        if (digits != _digits)
            return DataResult<int>.Error(() => $"Hex color is wrong size, expected {_digits} digits but got {digits}");
        var maxValue = (1L << (_digits * 4)) - 1L;
        if (!long.TryParse(text.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value) || value > maxValue)
            return DataResult<int>.Error(() => $"Invalid color value: {text}");
        var color = (int)value;
        return DataResult<int>.Success(_rgb ? Argb.Opaque(color) : color);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, int value)
        => DataResult<U>.Success(ops.CreateString(_rgb
            ? "#" + Argb.Transparent(value).ToString("x6", CultureInfo.InvariantCulture)
            : "#" + value.ToString("x8", CultureInfo.InvariantCulture)));
}

//SoundEventIdCodec 音效 id 弱引用对应原版 SoundEvent.CODEC 的注册表引用形态
//本仓库暂无声效注册表数据，直接按 Identifier 弱引用
internal sealed class SoundEventIdCodec : ScalarCodec<Identifier>
{
    public static readonly SoundEventIdCodec Instance = new();

    public override DataResult<Identifier> Parse<U>(DynamicOps<U> ops, U input)
    {
        var idResult = IdentifierCodec.Instance.Parse(ops, input);
        if (idResult.Result().IsPresent) return idResult;
        return ops.GetMap(input).FlatMap(map =>
        {
            var soundId = map.Get("sound_id");
            return soundId.IsPresent
                ? IdentifierCodec.Instance.Parse(ops, soundId.Get())
                : DataResult<Identifier>.Error(() => "Missing key sound_id");
        });
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Identifier value)
        => DataResult<U>.Success(ops.CreateString(value.ToString()));
}

//ParticleIdCodec 粒子 id 弱引用，接受 id 字符串或带 type 的粒子对象
internal sealed class ParticleIdCodec : ScalarCodec<Identifier>
{
    public static readonly ParticleIdCodec Instance = new();

    public override DataResult<Identifier> Parse<U>(DynamicOps<U> ops, U input)
    {
        var idResult = IdentifierCodec.Instance.Parse(ops, input);
        if (idResult.Result().IsPresent) return idResult;
        return ops.GetMap(input).FlatMap(map =>
        {
            var type = map.Get("type");
            return type.IsPresent
                ? IdentifierCodec.Instance.Parse(ops, type.Get())
                : DataResult<Identifier>.Error(() => "Missing key type");
        });
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Identifier value)
        => DataResult<U>.Success(ops.CreateString(value.ToString()));
}

//AttributeValueCodecs 环境属性常用取值 codec
internal static class AttributeValueCodecs
{
    //UnitFloat [0,1] 浮点
    public static readonly Codec<float> UnitFloat = new ValidatingCodec<float>(Codecs.Float,
        value => value >= 0.0f && value <= 1.0f
            ? DataResult<float>.Success(value)
            : DataResult<float>.Error(() => $"{value} is not in range [0; 1]"));

    //NonNegativeInt 非负整数对应原版 ExtraCodecs.NON_NEGATIVE_INT
    public static readonly Codec<int> NonNegativeInt = new ValidatingCodec<int>(Codecs.Int,
        value => value >= 0
            ? DataResult<int>.Success(value)
            : DataResult<int>.Error(() => $"Value must be non-negative: {value}"));
}
