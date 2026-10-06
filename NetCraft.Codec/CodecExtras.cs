namespace NetCraft.Codec;

//long[] codec, mirroring vanilla Codec.LONG_STREAM
//Serializes into a ListTag<LongTag> and deserializes longs from the stream
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

//Codec extension methods, mirroring vanilla Codec.mapResult/lenientOptionalFieldOf
public static class CodecExtras
{
    //long[] codec instance, mirroring vanilla Codec.LONG_STREAM
    public static readonly Codec<long[]> LongArray = new LongArrayCodec();

    //Mirrors vanilla ExtraCodecs.orElsePartial
    //A failed parse is replaced by the default value instead of throwing
    public static Codec<T> MapResult<T>(this Codec<T> codec, T defaultValue)
        => new MapResultCodec<T>(codec, defaultValue);

    //Mirrors vanilla Codec.lenientOptionalFieldOf
    //A missing field returns Empty and a parse error also returns Empty without reporting
    public static MapCodec<Optional<T>> LenientOptionalFieldOf<T>(this Codec<T> codec, string name)
        => new LenientOptionalFieldCodec<T>(name, codec);
}

//MapResult codec, mirroring vanilla mapResult(orElsePartial)
//A failed parse falls back to the default value and encode passes through
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

//Lenient optional field codec, mirroring vanilla lenientOptionalFieldOf
//Both a missing field and a parse error return Optional.Empty instead of throwing
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
