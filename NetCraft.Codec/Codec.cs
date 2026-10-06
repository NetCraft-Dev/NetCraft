namespace NetCraft.Codec;

//Main serialization interface, mirroring vanilla com.mojang.serialization.Codec
//Extends MapCodec and adds Parse, which takes a single element instead of a MapLike
public interface Codec<T> : MapCodec<T>
{
    //Decode from a single element, mirroring vanilla Codec.decode
    DataResult<T> Parse<U>(DynamicOps<U> ops, U input);

    //Encode into a single element, mirroring vanilla Codec.encodeStart
    //Same signature as MapCodec.EncodeStart, so Codec does not redeclare it

    //List codec
    Codec<IReadOnlyList<T>> ListOf();

    //Convert codec, mirroring vanilla comapFlatMap
    //to converts the current type into the new one, from goes the other way
    Codec<R> ComapFlatMap<R>(Func<T, DataResult<R>> to, Func<R, T> from);
}

//Abstract Codec base class providing default MapCodec methods that throw NotSupportedException
//A scalar codec only needs to override Parse and EncodeStart
public abstract class ScalarCodec<T> : Codec<T>
{
    public virtual DataResult<T> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => DataResult<T>.Error(() => $"{typeof(T).Name} codec does not support map decode");

    public virtual DataResult<U> EncodeStart<U>(DynamicOps<U> ops, T value)
        => DataResult<U>.Error(() => $"{typeof(T).Name} codec does not support encode");

    //Scalar codecs do not support EncodeTo accumulation
    public RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, T value, RecordBuilder<U> builder)
        => throw new NotSupportedException($"{typeof(T).Name} codec does not support record builder");

    public RecordBuilder<U> Encoder<U>(DynamicOps<U> ops)
        => throw new NotSupportedException($"{typeof(T).Name} codec does not support record builder");

    public Codec<IReadOnlyList<T>> ListOf() => new ListCodec<T>(this);

    public Codec<R> ComapFlatMap<R>(Func<T, DataResult<R>> to, Func<R, T> from)
        => new ComapFlatMapCodec<T, R>(this, to, from);

    public abstract DataResult<T> Parse<U>(DynamicOps<U> ops, U input);
}

//Abstract MapCodec base class providing a default EncodeStart built on EncodeTo
//Subclasses only implement Decode and EncodeTo
public abstract class AbstractMapCodec<T> : Codec<T>
{
    //The default implementation accumulates into a builder with EncodeTo and then calls Build(empty)
    public virtual DataResult<U> EncodeStart<U>(DynamicOps<U> ops, T value)
    {
        var builder = ops.MapBuilder();
        EncodeTo(ops, value, builder);
        return builder.Build(ops.Empty());
    }

    public Codec<IReadOnlyList<T>> ListOf() => new ListCodec<T>(this);

    public Codec<R> ComapFlatMap<R>(Func<T, DataResult<R>> to, Func<R, T> from)
        => new ComapFlatMapCodec<T, R>(this, to, from);

    public abstract DataResult<T> Decode<U>(DynamicOps<U> ops, MapLike<U> input);

    public abstract RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, T value, RecordBuilder<U> builder);

    public RecordBuilder<U> Encoder<U>(DynamicOps<U> ops)
        => ops.MapBuilder();

    //Parse goes through GetMap and then Decode, reusing the map decoding logic
    public virtual DataResult<T> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => Decode(ops, map));
}

//List codec, mirroring vanilla Codec.listOf
internal sealed class ListCodec<T> : ScalarCodec<IReadOnlyList<T>>
{
    private readonly Codec<T> _elementCodec;

    public ListCodec(Codec<T> elementCodec) { _elementCodec = elementCodec; }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, IReadOnlyList<T> value)
    {
        var stream = value.Select(t => _elementCodec.EncodeStart(ops, t).GetOrThrow());
        return DataResult<U>.Success(ops.CreateList(stream));
    }

    public override DataResult<IReadOnlyList<T>> Parse<U>(DynamicOps<U> ops, U input)
    {
        return ops.GetStream(input).Map(stream =>
            (IReadOnlyList<T>)stream.Select(t => _elementCodec.Parse(ops, t).GetOrThrow()).ToList());
    }
}

//Convert codec, mirroring vanilla Codec.comapFlatMap
internal sealed class ComapFlatMapCodec<T, R> : ScalarCodec<R>
{
    private readonly Codec<T> _source;
    private readonly Func<T, DataResult<R>> _to;
    private readonly Func<R, T> _from;

    public ComapFlatMapCodec(Codec<T> source, Func<T, DataResult<R>> to, Func<R, T> from)
    {
        _source = source;
        _to = to;
        _from = from;
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, R value)
        => _source.EncodeStart(ops, _from(value));

    public override DataResult<R> Parse<U>(DynamicOps<U> ops, U input)
        => _source.Parse(ops, input).FlatMap(_to);
}
