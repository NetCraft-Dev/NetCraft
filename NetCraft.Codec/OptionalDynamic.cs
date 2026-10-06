namespace NetCraft.Codec;

//Optional Dynamic, mirroring vanilla com.mojang.serialization.OptionalDynamic
//Wraps the value returned by Dynamic.Get; the inner DataResult holds a value on success and an error message on failure
//Forwards convenience methods such as AsNumber/AsString to the inner value on success, or returns a default
public sealed class OptionalDynamic<T>
{
    public DynamicOps<T> Ops { get; }

    //The inner DataResult carries a key-missing error message on failure
    public DataResult<T> Inner { get; }

    public OptionalDynamic(DynamicOps<T> ops, DataResult<T> inner)
    {
        Ops = ops;
        Inner = inner;
    }

    //On success it wraps the value as a Dynamic and returns Optional, on failure Empty
    public Optional<Dynamic<T>> Result()
        => Inner.Result().Map(v => new Dynamic<T>(Ops, v));

    //Returns the wrapped value on success and other on failure
    public Dynamic<T> OrElse(Dynamic<T> other)
        => Result().OrElse(other);

    //Returns the wrapped value on success and throws on failure
    public Dynamic<T> GetOrThrow()
        => new(Ops, Inner.GetOrThrow(err => new InvalidOperationException(err)));

    //===convenience methods forwarded to Dynamic, returning a default on failure===

    public DataResult<double> AsNumber() => Result().Map(d => d.AsNumber()).OrElse(DataResult<double>.Error(() => "Empty"));

    public double AsNumber(double def) => Result().Map(d => d.AsNumber(def)).OrElse(def);

    public DataResult<string> AsString() => Result().Map(d => d.AsString()).OrElse(DataResult<string>.Error(() => "Empty"));

    public string AsString(string def) => Result().Map(d => d.AsString(def)).OrElse(def);

    public int AsInt(int def) => (int)AsNumber(def);

    public long AsLong(long def) => (long)AsNumber(def);

    public float AsFloat(float def) => (float)AsNumber(def);

    public double AsDouble(double def) => AsNumber(def);

    public bool AsBoolean(bool def) => Result().Map(d => d.AsBoolean(def)).OrElse(def);

    //asStream forwards to the inner Dynamic's AsStream and returns an empty DataResult on failure
    public DataResult<IEnumerable<Dynamic<T>>> AsStream()
        => Result().Map(d => d.AsStream()).OrElse(DataResult<IEnumerable<Dynamic<T>>>.Error(() => "Empty"));

    //asStreamOpt returns an empty collection on failure, matching vanilla OptionalDynamic.asStream().result().orElse(Stream.empty())
    public IEnumerable<Dynamic<T>> AsStreamOpt()
        => Result().Map(d => d.AsStreamOpt()).OrElse(Enumerable.Empty<Dynamic<T>>());
}
