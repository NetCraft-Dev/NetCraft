namespace NetCraft.Codec;

//long[] codec对应原版Codec.LONG_STREAM
//序列化为ListTag<LongTag>反序列化从stream取long
internal sealed class LongArrayCodec : ScalarCodec<long[]>
{
    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, long[] value)
    {
        var stream = value.Select(v => ops.CreateLong(v));
        return DataResult<U>.Success(ops.CreateList(stream));
    }

    public override DataResult<long[]> Parse<U>(DynamicOps<U> ops, U input)
    {
        return ops.GetStream(input).Map(stream =>
            stream.Select(t => ops.GetLongValue(t).GetOrThrow()).ToArray());
    }
}

//Codec扩展方法对应原版Codec.mapResult/lenientOptionalFieldOf
public static class CodecExtras
{
    //long[] codec实例对应原版Codec.LONG_STREAM
    public static readonly Codec<long[]> LongArray = new LongArrayCodec();

    //对应原版ExtraCodecs.orElsePartial
    //解析失败用默认值替代不抛错
    public static Codec<T> MapResult<T>(this Codec<T> codec, T defaultValue)
        => new MapResultCodec<T>(codec, defaultValue);

    //对应原版Codec.lenientOptionalFieldOf
    //字段缺失返回Empty解析错误也返回Empty不报错
    public static MapCodec<Optional<T>> LenientOptionalFieldOf<T>(this Codec<T> codec, string name)
        => new LenientOptionalFieldCodec<T>(name, codec);
}

//MapResult codec对应原版mapResult(orElsePartial)
//parse失败用默认值替代encode透传
internal sealed class MapResultCodec<T> : ScalarCodec<T>
{
    private readonly Codec<T> _delegate;
    private readonly T _default;

    public MapResultCodec(Codec<T> codec, T defaultValue)
    {
        _delegate = codec;
        _default = defaultValue;
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, T value)
        => _delegate.EncodeStart(ops, value);

    public override DataResult<T> Parse<U>(DynamicOps<U> ops, U input)
        => _delegate.Parse(ops, input).ResultOrPartial(_ => { }).IsPresent
            ? _delegate.Parse(ops, input)
            : DataResult<T>.Success(_default!);
}

//lenient optional field codec对应原版lenientOptionalFieldOf
//缺失或解析错误都返回Optional.Empty不抛
internal sealed class LenientOptionalFieldCodec<T> : AbstractMapCodec<Optional<T>>
{
    private readonly string _name;
    private readonly Codec<T> _elementCodec;

    public LenientOptionalFieldCodec(string name, Codec<T> elementCodec)
    {
        _name = name;
        _elementCodec = elementCodec;
    }

    public override DataResult<Optional<T>> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var value = input.Get(_name);
        if (!value.IsPresent)
            return DataResult<Optional<T>>.Success(Optional<T>.Empty());
        var parsed = _elementCodec.Parse(ops, value.Get());
        return parsed.ResultOrPartial(_ => { }).IsPresent
            ? parsed.Map(Optional<T>.Of)
            : DataResult<Optional<T>>.Success(Optional<T>.Empty());
    }

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, Optional<T> value, RecordBuilder<U> builder)
    {
        if (value.IsPresent)
            builder.Add(_name, _elementCodec.EncodeStart(ops, value.Get()).GetOrThrow());
        return builder;
    }
}
