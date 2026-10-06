namespace NetCraft.Codec;

//Serialization result, mirroring vanilla com.mojang.serialization.DataResult
//Carries a value on success, an error message on failure and an optional partial value
public sealed class DataResult<T>
{
    private readonly T? _value;
    private readonly string? _error;
    private readonly bool _success;

    private DataResult(T? value, string? error, bool success)
    {
        _value = value;
        _error = error;
        _success = success;
    }

    public static DataResult<T> Success(T value) => new(value, null, true);

    public static DataResult<T> Error(Func<string> message) => new(default, message(), false);

    public static DataResult<T> Error(Func<string> message, T? partialValue)
        => new(partialValue, message(), false);

    //Returns the value on success and Empty on failure
    public Optional<T> Result() => _success ? Optional<T>.OfNullable(_value) : Optional<T>.Empty();

    //On failure it invokes errorHandler and returns the partial value if there is one
    public Optional<T> ResultOrPartial(Action<string>? errorHandler = null)
    {
        if (_error is not null) errorHandler?.Invoke(_error);
        return _value is not null ? Optional<T>.Of(_value) : Optional<T>.Empty();
    }

    public T GetOrThrow()
        => _success && _value is not null
            ? _value
            : throw new InvalidOperationException(_error ?? "DataResult had no value");

    public T GetOrThrow(Func<string> message)
        => _success && _value is not null ? _value : throw new InvalidOperationException(message());

    //On failure it throws a custom exception, mirroring vanilla getOrThrow(ChunkReadException::new)
    public T GetOrThrow(Func<string, Exception> exceptionFactory)
        => _success && _value is not null ? _value : throw exceptionFactory(_error ?? "DataResult had no value");

    //On failure it returns the partial value when present and throws otherwise, mirroring vanilla getPartialOrThrow
    public T GetPartialOrThrow()
        => _value is not null
            ? _value
            : throw new InvalidOperationException(_error ?? "DataResult had no value");

    //On failure it returns the partial value when present, otherwise throws the exception built by exceptionFactory
    public T GetPartialOrThrow(Func<string, Exception> exceptionFactory)
        => _value is not null ? _value : throw exceptionFactory(_error ?? "DataResult had no value");

    public DataResult<R> Map<R>(Func<T, R> mapper)
        => _success
            ? DataResult<R>.Success(mapper(_value!))
            : DataResult<R>.Error(() => _error!, default);

    public DataResult<R> FlatMap<R>(Func<T, DataResult<R>> mapper)
        => _success ? mapper(_value!) : DataResult<R>.Error(() => _error!, default);

    //Maps with success on success and returns failure's value on failure, mirroring vanilla mapOrElse
    public R MapOrElse<R>(Func<T, R> success, Func<string, R> failure)
        => _success ? success(_value!) : failure(_error!);
}
