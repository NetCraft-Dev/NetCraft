namespace NetCraft.Codec;

//fieldOf(name) implementation, mirroring vanilla FieldCodec
//Decode takes the name field from the MapLike and EncodeTo Adds the field value to the builder
internal sealed class FieldMapCodec<T> : AbstractMapCodec<T>
{
    private readonly string _name;
    private readonly Codec<T> _elementCodec;

    public FieldMapCodec(string name, Codec<T> elementCodec)
    {
        _name = name;
        _elementCodec = elementCodec;
    }

    //A missing field returns an Error, matching the vanilla required-field behavior
    public override DataResult<T> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var value = input.Get(_name);
        return value.IsPresent
            ? _elementCodec.Parse(ops, value.Get())
            : DataResult<T>.Error(() => $"Missing key {_name}");
    }

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, T value, RecordBuilder<U> builder)
    {
        builder.Add(_name, _elementCodec.EncodeStart(ops, value).GetOrThrow());
        return builder;
    }
}

//optionalFieldOf(name, default), mirroring vanilla OptionalFieldCodec with a default value
//A missing field uses default and EncodeTo always writes
internal sealed class OptionalFieldMapCodec<T> : AbstractMapCodec<T>
{
    private readonly string _name;
    private readonly Codec<T> _elementCodec;
    private readonly T _default;

    public OptionalFieldMapCodec(string name, Codec<T> elementCodec, T defaultValue)
    {
        _name = name;
        _elementCodec = elementCodec;
        _default = defaultValue;
    }

    public override DataResult<T> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var value = input.Get(_name);
        return value.IsPresent
            ? _elementCodec.Parse(ops, value.Get())
            : DataResult<T>.Success(_default!);
    }

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, T value, RecordBuilder<U> builder)
    {
        builder.Add(_name, _elementCodec.EncodeStart(ops, value).GetOrThrow());
        return builder;
    }
}

//optionalFieldOf(name) without a default returns Optional<T>, mirroring vanilla OptionalFieldCodec
//A missing field returns Optional.Empty
internal sealed class OptionalFieldMapCodecOptional<T> : AbstractMapCodec<Optional<T>>
{
    private readonly string _name;
    private readonly Codec<T> _elementCodec;

    public OptionalFieldMapCodecOptional(string name, Codec<T> elementCodec)
    {
        _name = name;
        _elementCodec = elementCodec;
    }

    public override DataResult<Optional<T>> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var value = input.Get(_name);
        return value.IsPresent
            ? _elementCodec.Parse(ops, value.Get()).Map(Optional<T>.Of)
            : DataResult<Optional<T>>.Success(Optional<T>.Empty());
    }

    //An empty Optional writes no field, matching vanilla's ignoring of empty values
    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, Optional<T> value, RecordBuilder<U> builder)
    {
        if (value.IsPresent)
            builder.Add(_name, _elementCodec.EncodeStart(ops, value.Get()).GetOrThrow());
        return builder;
    }
}
