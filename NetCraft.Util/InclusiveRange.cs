using NetCraft.Codec;

namespace NetCraft.Util;

//InclusiveRange closed range, maps to vanilla net.minecraft.util.InclusiveRange
public sealed record InclusiveRange<T> where T : IComparable<T>
{
    public T MinInclusive { get; }
    public T MaxInclusive { get; }

    public InclusiveRange(T minInclusive, T maxInclusive)
    {
        if (minInclusive.CompareTo(maxInclusive) > 0)
            throw new ArgumentException("min_inclusive must be less than or equal to max_inclusive");
        MinInclusive = minInclusive;
        MaxInclusive = maxInclusive;
    }

    public InclusiveRange(T value) : this(value, value) { }

    //Codec mirrors vanilla InclusiveRange.codec(elementCodec), decoding the two-element array form
    public static Codec<InclusiveRange<T>> Codec(Codec<T> elementCodec) => new InclusiveRangeCodec<T>(elementCodec);

    //Codec mirrors vanilla InclusiveRange.codec(elementCodec, min, max), rejecting ranges outside the allowed bounds
    public static Codec<InclusiveRange<T>> Codec(Codec<T> elementCodec, T minAllowedInclusive, T maxAllowedInclusive)
        => Codec(elementCodec).Validate(value =>
        {
            if (value.MinInclusive.CompareTo(minAllowedInclusive) < 0)
                return DataResult<InclusiveRange<T>>.Error(() =>
                    $"Range limit too low, expected at least {minAllowedInclusive} [{value.MinInclusive}-{value.MaxInclusive}]");
            if (value.MaxInclusive.CompareTo(maxAllowedInclusive) > 0)
                return DataResult<InclusiveRange<T>>.Error(() =>
                    $"Range limit too high, expected at most {maxAllowedInclusive} [{value.MinInclusive}-{value.MaxInclusive}]");
            return DataResult<InclusiveRange<T>>.Success(value);
        });

    //Create fallible construction, maps to vanilla InclusiveRange.create
    public static DataResult<InclusiveRange<T>> Create(T minInclusive, T maxInclusive)
        => minInclusive.CompareTo(maxInclusive) <= 0
            ? DataResult<InclusiveRange<T>>.Success(new InclusiveRange<T>(minInclusive, maxInclusive))
            : DataResult<InclusiveRange<T>>.Error(() => "min_inclusive must be less than or equal to max_inclusive");

    public InclusiveRange<S> Map<S>(Func<T, S> mapper) where S : IComparable<S>
        => new(mapper(MinInclusive), mapper(MaxInclusive));

    public bool IsValueInRange(T value)
        => value.CompareTo(MinInclusive) >= 0 && value.CompareTo(MaxInclusive) <= 0;

    public bool Contains(InclusiveRange<T> subRange)
        => subRange.MinInclusive.CompareTo(MinInclusive) >= 0 && subRange.MaxInclusive.CompareTo(MaxInclusive) <= 0;

    public override string ToString() => $"[{MinInclusive}, {MaxInclusive}]";
}

//InclusiveRangeCodec serializes the range as a two-element array, matching the pack.mcmeta supported_formats form
internal sealed class InclusiveRangeCodec<T> : ScalarCodec<InclusiveRange<T>> where T : IComparable<T>
{
    private readonly Codec<T> _elementCodec;

    public InclusiveRangeCodec(Codec<T> elementCodec) => _elementCodec = elementCodec;

    public override DataResult<InclusiveRange<T>> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetStream(input).FlatMap(stream =>
        {
            var bounds = new List<T>();
            foreach (var element in stream)
                bounds.Add(_elementCodec.Parse(ops, element).GetOrThrow());
            return bounds.Count == 2
                ? InclusiveRange<T>.Create(bounds[0], bounds[1])
                : DataResult<InclusiveRange<T>>.Error(() => "range must be a two-element array");
        });

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, InclusiveRange<T> value)
        => DataResult<U>.Success(ops.CreateList(new[]
        {
            _elementCodec.EncodeStart(ops, value.MinInclusive).GetOrThrow(),
            _elementCodec.EncodeStart(ops, value.MaxInclusive).GetOrThrow()
        }));
}
