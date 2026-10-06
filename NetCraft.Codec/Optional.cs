namespace NetCraft.Codec;

//Lightweight Optional, mirroring vanilla java.util.Optional
//.NET has no built-in Optional, and this type avoids confusion with T?
public readonly struct Optional<T>
{
    private readonly T? _value;
    public bool IsPresent { get; }

    private Optional(T value, bool present)
    {
        _value = value;
        IsPresent = present;
    }

    public static Optional<T> Empty() => default;

    public static Optional<T> Of(T value) => new(value, true);

    public static Optional<T> OfNullable(T? value) => value is null ? Empty() : Of(value);

    public T Get() => IsPresent ? _value! : throw new InvalidOperationException("No value present");

    public T OrElse(T other) => IsPresent ? _value! : other;

    public Optional<R> Map<R>(Func<T, R> mapper)
        => IsPresent ? Optional<R>.Of(mapper(_value!)) : Optional<R>.Empty();

    //Returns a single-element sequence when present and an empty one otherwise, mirroring vanilla Optional.stream
    public IEnumerable<T> Stream() => IsPresent ? new[] { _value! } : Enumerable.Empty<T>();
}
