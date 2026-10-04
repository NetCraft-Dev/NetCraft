namespace NetCraft.Codec;

//fieldOf(name)实现对应原版FieldCodec
//Decode从MapLike取name字段EncodeTo把字段值Add到builder
internal sealed class FieldMapCodec<T> : AbstractMapCodec<T>
{
    private readonly string _name;
    private readonly Codec<T> _elementCodec;

    public FieldMapCodec(string name, Codec<T> elementCodec)
    {
        _name = name;
        _elementCodec = elementCodec;
    }

    //字段缺失返回Error对应原版必填字段行为
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

//optionalFieldOf(name, default)对应原版OptionalFieldCodec带默认值
//字段缺失用defaultEncodeTo总是写入
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

//optionalFieldOf(name)无default返回Optional<T>对应原版OptionalFieldCodec
//字段缺失返回Optional.Empty
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

    //空Optional不写字段对应原版忽略空值
    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, Optional<T> value, RecordBuilder<U> builder)
    {
        if (value.IsPresent)
            builder.Add(_name, _elementCodec.EncodeStart(ops, value.Get()).GetOrThrow());
        return builder;
    }
}
