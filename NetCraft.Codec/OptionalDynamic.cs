namespace NetCraft.Codec;

//可选Dynamic对应原版com.mojang.serialization.OptionalDynamic
//Dynamic.Get返回值包装内部DataResult成功有值失败带错误信息
//转发AsNumber/AsString等便捷方法到内部成功值或返回默认
public sealed class OptionalDynamic<T>
{
    public DynamicOps<T> Ops { get; }

    //内部DataResult失败时携带Key不存在的错误信息
    public DataResult<T> Inner { get; }

    public OptionalDynamic(DynamicOps<T> ops, DataResult<T> inner)
    {
        Ops = ops;
        Inner = inner;
    }

    //成功时包装为Dynamic返回Optional失败返回Empty
    public Optional<Dynamic<T>> Result()
        => Inner.Result().Map(v => new Dynamic<T>(Ops, v));

    //成功返回包装值失败返回other
    public Dynamic<T> OrElse(Dynamic<T> other)
        => Result().OrElse(other);

    //成功返回包装值失败抛异常
    public Dynamic<T> GetOrThrow()
        => new(Ops, Inner.GetOrThrow(err => new InvalidOperationException(err)));

    //===转发到Dynamic的便捷方法失败返回默认===

    public DataResult<double> AsNumber() => Result().Map(d => d.AsNumber()).OrElse(DataResult<double>.Error(() => "Empty"));

    public double AsNumber(double def) => Result().Map(d => d.AsNumber(def)).OrElse(def);

    public DataResult<string> AsString() => Result().Map(d => d.AsString()).OrElse(DataResult<string>.Error(() => "Empty"));

    public string AsString(string def) => Result().Map(d => d.AsString(def)).OrElse(def);

    public int AsInt(int def) => (int)AsNumber(def);

    public long AsLong(long def) => (long)AsNumber(def);

    public float AsFloat(float def) => (float)AsNumber(def);

    public double AsDouble(double def) => AsNumber(def);

    public bool AsBoolean(bool def) => Result().Map(d => d.AsBoolean(def)).OrElse(def);

    //asStream转发到内部Dynamic的AsStream失败返回空DataResult
    public DataResult<IEnumerable<Dynamic<T>>> AsStream()
        => Result().Map(d => d.AsStream()).OrElse(DataResult<IEnumerable<Dynamic<T>>>.Error(() => "Empty"));

    //asStreamOpt失败时返回空集合对齐原版OptionalDynamic.asStream().result().orElse(Stream.empty())
    public IEnumerable<Dynamic<T>> AsStreamOpt()
        => Result().Map(d => d.AsStreamOpt()).OrElse(Enumerable.Empty<Dynamic<T>>());
}
