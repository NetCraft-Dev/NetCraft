namespace NetCraft.Codec;

//FlatXmapMapCodec remaps a MapCodec value type through a pair of fallible functions, mirroring vanilla MapCodec.flatXmap
//Decode runs to and may fail; encode runs from and may fail before delegating
internal sealed class FlatXmapMapCodec<T, R> : MapCodec<R>
{
    private readonly MapCodec<T> _source;
    private readonly Func<T, DataResult<R>> _to;
    private readonly Func<R, DataResult<T>> _from;

    public FlatXmapMapCodec(MapCodec<T> source, Func<T, DataResult<R>> to, Func<R, DataResult<T>> from)
    {
        _source = source;
        _to = to;
        _from = from;
    }

    public DataResult<R> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => _source.Decode(ops, input).FlatMap(_to);

    public DataResult<U> EncodeStart<U>(DynamicOps<U> ops, R value)
        => _from(value).FlatMap(t => _source.EncodeStart(ops, t));

    public RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, R value, RecordBuilder<U> builder)
        => _source.EncodeTo(ops, _from(value).GetOrThrow(), builder);

    public RecordBuilder<U> Encoder<U>(DynamicOps<U> ops) => _source.Encoder(ops);
}

//ValidateCodec runs a checker over the value on both decode and encode, mirroring vanilla Codec.validate
//The checker returns the value it accepts, so a normalizing check can rewrite it
internal sealed class ValidateCodec<T> : Codec<T>
{
    private readonly Codec<T> _source;
    private readonly Func<T, DataResult<T>> _checker;

    public ValidateCodec(Codec<T> source, Func<T, DataResult<T>> checker)
    {
        _source = source;
        _checker = checker;
    }

    public DataResult<T> Parse<U>(DynamicOps<U> ops, U input)
        => _source.Parse(ops, input).FlatMap(_checker);

    public DataResult<T> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => _source.Decode(ops, input).FlatMap(_checker);

    public DataResult<U> EncodeStart<U>(DynamicOps<U> ops, T value)
        => _checker(value).FlatMap(v => _source.EncodeStart(ops, v));

    public RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, T value, RecordBuilder<U> builder)
        => _source.EncodeTo(ops, _checker(value).GetOrThrow(), builder);

    public RecordBuilder<U> Encoder<U>(DynamicOps<U> ops) => _source.Encoder(ops);

    public Codec<IReadOnlyList<T>> ListOf() => new ListCodec<T>(this);

    public Codec<R> ComapFlatMap<R>(Func<T, DataResult<R>> to, Func<R, T> from)
        => new ComapFlatMapCodec<T, R>(this, to, from);
}
