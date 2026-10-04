namespace NetCraft.Codec;

//主序列化接口对应原版com.mojang.serialization.Codec
//继承MapCodec并增加Parse接收单个元素而非MapLike
public interface Codec<T> : MapCodec<T>
{
    //从单个元素解码对应原版Codec.decode
    DataResult<T> Parse<U>(DynamicOps<U> ops, U input);

    //编码为单个元素对应原版Codec.encodeStart
    //与MapCodec.EncodeStart签名相同Codec不重复声明

    //列表codec
    Codec<IReadOnlyList<T>> ListOf();

    //转换codec对应原版comapFlatMap
    //to把当前类型转新类型from反向
    Codec<R> ComapFlatMap<R>(Func<T, DataResult<R>> to, Func<R, T> from);
}

//Codec抽象基类提供MapCodec方法的默认抛NotSupportedException实现
//标量codec只需重写Parse和EncodeStart
public abstract class ScalarCodec<T> : Codec<T>
{
    public virtual DataResult<T> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => DataResult<T>.Error(() => $"{typeof(T).Name} codec does not support map decode");

    public virtual DataResult<U> EncodeStart<U>(DynamicOps<U> ops, T value)
        => DataResult<U>.Error(() => $"{typeof(T).Name} codec does not support encode");

    //标量codec不支持EncodeTo累积
    public RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, T value, RecordBuilder<U> builder)
        => throw new NotSupportedException($"{typeof(T).Name} codec does not support record builder");

    public RecordBuilder<U> Encoder<U>(DynamicOps<U> ops)
        => throw new NotSupportedException($"{typeof(T).Name} codec does not support record builder");

    public Codec<IReadOnlyList<T>> ListOf() => new ListCodec<T>(this);

    public Codec<R> ComapFlatMap<R>(Func<T, DataResult<R>> to, Func<R, T> from)
        => new ComapFlatMapCodec<T, R>(this, to, from);

    public abstract DataResult<T> Parse<U>(DynamicOps<U> ops, U input);
}

//抽象MapCodec基类提供EncodeStart默认实现基于EncodeTo
//子类只需实现Decode和EncodeTo
public abstract class AbstractMapCodec<T> : Codec<T>
{
    //默认实现用EncodeTo累积到builder再Build(empty)
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

    //Parse走GetMap再Decode复用map解码逻辑
    public virtual DataResult<T> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => Decode(ops, map));
}

//列表codec对应原版Codec.listOf
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

//转换codec对应原版Codec.comapFlatMap
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
