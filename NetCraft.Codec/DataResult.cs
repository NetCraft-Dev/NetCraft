namespace NetCraft.Codec;

//序列化结果对应原版com.mojang.serialization.DataResult
//成功携带值失败携带错误消息可选partial值
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

    //仅成功时返回值失败返回Empty
    public Optional<T> Result() => _success ? Optional<T>.OfNullable(_value) : Optional<T>.Empty();

    //失败时回调errorHandler并返回可能存在的partial值
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

    //失败抛自定义异常对应原版getOrThrow(ChunkReadException::new)
    public T GetOrThrow(Func<string, Exception> exceptionFactory)
        => _success && _value is not null ? _value : throw exceptionFactory(_error ?? "DataResult had no value");

    //失败但有partial值时返回partial失败且无partial抛异常对应原版getPartialOrThrow
    public T GetPartialOrThrow()
        => _value is not null
            ? _value
            : throw new InvalidOperationException(_error ?? "DataResult had no value");

    //失败但有partial值时返回partial否则抛exceptionFactory构造的异常
    public T GetPartialOrThrow(Func<string, Exception> exceptionFactory)
        => _value is not null ? _value : throw exceptionFactory(_error ?? "DataResult had no value");

    public DataResult<R> Map<R>(Func<T, R> mapper)
        => _success
            ? DataResult<R>.Success(mapper(_value!))
            : DataResult<R>.Error(() => _error!, default);

    public DataResult<R> FlatMap<R>(Func<T, DataResult<R>> mapper)
        => _success ? mapper(_value!) : DataResult<R>.Error(() => _error!, default);

    //成功映射success失败用failure返回值对应原版mapOrElse
    public R MapOrElse<R>(Func<T, R> success, Func<string, R> failure)
        => _success ? success(_value!) : failure(_error!);
}
