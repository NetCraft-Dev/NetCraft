using NetCraft.Codec;
using NetCraft.Registry;
using NetCraft.Util;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen;

//IntProvider int provider system, maps to vanilla net.minecraft.util.valueproviders
//IntProviders.Codec is either a bare int or dispatch by the type field; the type set is closed, so dispatch is hand-written like FloatProviders
public abstract class IntProvider : NetCraft.Registry.IntProvider
{
    //Sample draws an int from the random source, maps to vanilla sample
    public abstract int Sample(RandomSource random);

    //MinInclusive lower bound, maps to vanilla minInclusive
    public abstract int MinInclusive { get; }

    //MaxInclusive upper bound, maps to vanilla maxInclusive
    public abstract int MaxInclusive { get; }
}

//ConstantInt constant int provider, maps to vanilla ConstantInt
public sealed class ConstantInt : IntProvider
{
    //Zero constant-zero singleton, maps to vanilla ZERO
    public static readonly ConstantInt Zero = new(0);

    public int Value { get; }

    public ConstantInt(int value) => Value = value;

    //Of reuses the singleton for zero, maps to vanilla of
    public static ConstantInt Of(int value) => value == 0 ? Zero : new ConstantInt(value);

    public override int Sample(RandomSource random) => Value;

    public override int MinInclusive => Value;

    public override int MaxInclusive => Value;

    public override string ToString() => Value.ToString();
}

//UniformInt uniform distribution int provider, maps to vanilla UniformInt
public sealed class UniformInt : IntProvider
{
    public UniformInt(int minInclusive, int maxInclusive)
    {
        MinInclusive = minInclusive;
        MaxInclusive = maxInclusive;
    }

    public static UniformInt Of(int minInclusive, int maxInclusive) => new(minInclusive, maxInclusive);

    public override int MinInclusive { get; }

    public override int MaxInclusive { get; }

    public override int Sample(RandomSource random) => Mth.RandomBetweenInclusive(random, MinInclusive, MaxInclusive);

    public override string ToString() => $"[{MinInclusive}-{MaxInclusive}]";
}

//BiasedToBottomInt bottom-biased distribution int provider, maps to vanilla BiasedToBottomInt
public sealed class BiasedToBottomInt : IntProvider
{
    public BiasedToBottomInt(int minInclusive, int maxInclusive)
    {
        MinInclusive = minInclusive;
        MaxInclusive = maxInclusive;
    }

    public static BiasedToBottomInt Of(int minInclusive, int maxInclusive) => new(minInclusive, maxInclusive);

    public override int MinInclusive { get; }

    public override int MaxInclusive { get; }

    //Sample draws twice to bias toward lower values, maps to vanilla sample; the call order must not change
    public override int Sample(RandomSource random)
        => MinInclusive + random.NextInt(random.NextInt(MaxInclusive - MinInclusive + 1) + 1);

    public override string ToString() => $"[{MinInclusive}-{MaxInclusive}]";
}

//ClampedInt clamped int provider, maps to vanilla ClampedInt
public sealed class ClampedInt : IntProvider
{
    public IntProvider Source { get; }

    public ClampedInt(IntProvider source, int minInclusive, int maxInclusive)
    {
        Source = source;
        MinInclusive = minInclusive;
        MaxInclusive = maxInclusive;
    }

    public static ClampedInt Of(IntProvider source, int minInclusive, int maxInclusive)
        => new(source, minInclusive, maxInclusive);

    public override int MinInclusive { get; }

    public override int MaxInclusive { get; }

    public override int Sample(RandomSource random)
        => Mth.Clamp(Source.Sample(random), MinInclusive, MaxInclusive);
}

//ClampedNormalInt normal distribution then clamped int provider, maps to vanilla ClampedNormalInt
public sealed class ClampedNormalInt : IntProvider
{
    public float Mean { get; }
    public float Deviation { get; }

    public ClampedNormalInt(float mean, float deviation, int minInclusive, int maxInclusive)
    {
        Mean = mean;
        Deviation = deviation;
        MinInclusive = minInclusive;
        MaxInclusive = maxInclusive;
    }

    public static ClampedNormalInt Of(float mean, float deviation, int minInclusive, int maxInclusive)
        => new(mean, deviation, minInclusive, maxInclusive);

    public override int MinInclusive { get; }

    public override int MaxInclusive { get; }

    //Sample samples normally, then clamps, then truncates; the order matches vanilla
    public override int Sample(RandomSource random)
        => (int)Mth.Clamp(Mth.Normal(random, Mean, Deviation), MinInclusive, MaxInclusive);

    public override string ToString() => $"normal({Mean}, {Deviation}) in [{MinInclusive}-{MaxInclusive}]";
}

//TrapezoidInt trapezoid distribution int provider, maps to vanilla TrapezoidInt
public sealed class TrapezoidInt : IntProvider
{
    public int Plateau { get; }

    public TrapezoidInt(int minInclusive, int maxInclusive, int plateau)
    {
        MinInclusive = minInclusive;
        MaxInclusive = maxInclusive;
        Plateau = plateau;
    }

    public static TrapezoidInt Of(int min, int max, int plateau) => new(min, max, plateau);

    public static IntProvider Triangle(int range) => Of(-range, range, 0);

    public override int MinInclusive { get; }

    public override int MaxInclusive { get; }

    public override int Sample(RandomSource random)
    {
        //With symmetry and no plateau it degenerates to subtracting two independent draws, matching the vanilla fast path
        if (Plateau == 0 && MaxInclusive == -MinInclusive)
            return random.NextInt(MaxInclusive + 1) - random.NextInt(MaxInclusive + 1);
        var range = MaxInclusive - MinInclusive;
        if (Plateau == range) return Mth.RandomBetweenInclusive(random, MinInclusive, MaxInclusive);
        var plateauStart = (range - Plateau) / 2;
        var plateauEnd = range - plateauStart;
        return MinInclusive + Mth.RandomBetweenInclusive(random, 0, plateauEnd)
            + Mth.RandomBetweenInclusive(random, 0, plateauStart);
    }

    public override string ToString() => $"trapezoid({Plateau}) in [{MinInclusive}-{MaxInclusive}]";
}

//WeightedListInt weighted list int provider, maps to vanilla WeightedListInt
public sealed class WeightedListInt : IntProvider
{
    public WeightedList<IntProvider> Distribution { get; }

    public WeightedListInt(WeightedList<IntProvider> distribution)
    {
        Distribution = distribution;
        var min = int.MaxValue;
        var max = int.MinValue;
        foreach (var entry in distribution.Unwrap())
        {
            min = Math.Min(min, entry.Value.MinInclusive);
            max = Math.Max(max, entry.Value.MaxInclusive);
        }
        MinInclusive = min;
        MaxInclusive = max;
    }

    public override int MinInclusive { get; }

    public override int MaxInclusive { get; }

    public override int Sample(RandomSource random) => Distribution.GetRandomOrThrow(random).Sample(random);
}

//IntProviders int provider codec entry point, maps to vanilla IntProviders
public static class IntProviders
{
    //Codec either a bare int or an object with type, maps to vanilla CODEC
    public static readonly Codec<IntProvider> Codec = new IntProviderCodec();

    //NonNegativeCodec requires the whole range to be non-negative, maps to vanilla NON_NEGATIVE_CODEC
    public static readonly Codec<IntProvider> NonNegativeCodec = new RangeValidatedIntProviderCodec(Codec, 0, int.MaxValue);

    //PositiveCodec requires the whole range to be positive, maps to vanilla POSITIVE_CODEC
    public static readonly Codec<IntProvider> PositiveCodec = new RangeValidatedIntProviderCodec(Codec, 1, int.MaxValue);
}

//RangeValidatedIntProviderCodec adds range validation to an int provider
//Fields like count must not be negative; failing early beats computing a negative during generation and debugging that
internal sealed class RangeValidatedIntProviderCodec : ScalarCodec<IntProvider>
{
    private readonly Codec<IntProvider> _source;
    private readonly int _min;
    private readonly int _max;

    public RangeValidatedIntProviderCodec(Codec<IntProvider> source, int min, int max)
    {
        _source = source;
        _min = min;
        _max = max;
    }

    public override DataResult<IntProvider> Parse<U>(DynamicOps<U> ops, U input)
        => _source.Parse(ops, input).FlatMap(value =>
        {
            if (value.MinInclusive < _min)
                return DataResult<IntProvider>.Error(() =>
                    $"Min value is too small, expected {_min} but got [{value.MinInclusive}-{value.MaxInclusive}]");
            if (value.MaxInclusive > _max)
                return DataResult<IntProvider>.Error(() =>
                    $"Max value is too large, expected {_max} but got [{value.MinInclusive}-{value.MaxInclusive}]");
            return DataResult<IntProvider>.Success(value);
        });

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, IntProvider value)
        => _source.EncodeStart(ops, value);
}

//IntProviderCodec int provider codec; a bare number decodes to ConstantInt and an object dispatches by the type field
internal sealed class IntProviderCodec : ScalarCodec<IntProvider>
{
    public override DataResult<IntProvider> Parse<U>(DynamicOps<U> ops, U input)
    {
        var number = ops.GetNumberValue(input);
        if (number.Result().IsPresent)
            return DataResult<IntProvider>.Success(ConstantInt.Of((int)number.GetOrThrow()));
        return ops.GetMap(input).FlatMap(map => DecodeProvider(ops, map));
    }

    private static DataResult<IntProvider> DecodeProvider<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var type = ReadString(ops, input, "type");
        if (type is null) return DataResult<IntProvider>.Error(() => "IntProvider is missing the type field");
        switch (Identifier.TryParse(type)?.Path ?? type)
        {
            case "constant":
            {
                var value = ReadInt(ops, input, "value");
                return value is null
                    ? DataResult<IntProvider>.Error(() => "constant int provider is missing the value field")
                    : DataResult<IntProvider>.Success(ConstantInt.Of(value.Value));
            }
            case "uniform":
            {
                var min = ReadInt(ops, input, "min_inclusive");
                var max = ReadInt(ops, input, "max_inclusive");
                if (min is null || max is null)
                    return DataResult<IntProvider>.Error(() => "uniform requires min_inclusive and max_inclusive");
                if (max < min)
                    return DataResult<IntProvider>.Error(() => $"Max must not be less than min [{min}-{max}]");
                return DataResult<IntProvider>.Success(new UniformInt(min.Value, max.Value));
            }
            case "biased_to_bottom":
            {
                var min = ReadInt(ops, input, "min_inclusive");
                var max = ReadInt(ops, input, "max_inclusive");
                if (min is null || max is null)
                    return DataResult<IntProvider>.Error(() => "biased_to_bottom requires min_inclusive and max_inclusive");
                if (max < min)
                    return DataResult<IntProvider>.Error(() => $"Max must not be less than min [{min}-{max}]");
                return DataResult<IntProvider>.Success(new BiasedToBottomInt(min.Value, max.Value));
            }
            case "clamped":
            {
                var sourceTag = input.Get("source");
                if (!sourceTag.IsPresent)
                    return DataResult<IntProvider>.Error(() => "clamped is missing the source field");
                var source = IntProviders.Codec.Parse(ops, sourceTag.Get());
                if (!source.Result().IsPresent)
                    return DataResult<IntProvider>.Error(() => "failed to parse clamped source");
                var min = ReadInt(ops, input, "min_inclusive");
                var max = ReadInt(ops, input, "max_inclusive");
                if (min is null || max is null)
                    return DataResult<IntProvider>.Error(() => "clamped requires min_inclusive and max_inclusive");
                if (max < min)
                    return DataResult<IntProvider>.Error(() => $"Max must not be less than min [{min}-{max}]");
                return DataResult<IntProvider>.Success(new ClampedInt(source.GetOrThrow(), min.Value, max.Value));
            }
            case "clamped_normal":
            {
                var mean = ReadFloat(ops, input, "mean");
                var deviation = ReadFloat(ops, input, "deviation");
                var min = ReadInt(ops, input, "min_inclusive");
                var max = ReadInt(ops, input, "max_inclusive");
                if (mean is null || deviation is null || min is null || max is null)
                    return DataResult<IntProvider>.Error(() => "clamped_normal requires mean/deviation/min_inclusive/max_inclusive");
                if (max < min)
                    return DataResult<IntProvider>.Error(() => $"Max must not be less than min [{min}-{max}]");
                return DataResult<IntProvider>.Success(
                    new ClampedNormalInt(mean.Value, deviation.Value, min.Value, max.Value));
            }
            case "trapezoid":
            {
                var min = ReadInt(ops, input, "min");
                var max = ReadInt(ops, input, "max");
                var plateau = ReadInt(ops, input, "plateau");
                if (min is null || max is null || plateau is null)
                    return DataResult<IntProvider>.Error(() => "trapezoid requires min/max/plateau");
                if (max < min)
                    return DataResult<IntProvider>.Error(() => $"Max must not be less than min [{min}-{max}]");
                if (plateau > max - min)
                    return DataResult<IntProvider>.Error(() => $"Plateau can at most be the full span [{min}-{max}]");
                return DataResult<IntProvider>.Success(new TrapezoidInt(min.Value, max.Value, plateau.Value));
            }
            case "weighted_list":
            {
                var distributionTag = input.Get("distribution");
                if (!distributionTag.IsPresent)
                    return DataResult<IntProvider>.Error(() => "weighted_list is missing the distribution field");
                var stream = ops.GetStream(distributionTag.Get());
                if (!stream.Result().IsPresent)
                    return DataResult<IntProvider>.Error(() => "weighted_list distribution must be a list");
                var entries = new List<Weighted<IntProvider>>();
                foreach (var element in stream.GetOrThrow())
                {
                    var elementMap = ops.GetMap(element);
                    if (!elementMap.Result().IsPresent)
                        return DataResult<IntProvider>.Error(() => "weighted_list element must be an object");
                    var map = elementMap.GetOrThrow();
                    var dataTag = map.Get("data");
                    if (!dataTag.IsPresent)
                        return DataResult<IntProvider>.Error(() => "weighted_list element is missing the data field");
                    var data = IntProviders.Codec.Parse(ops, dataTag.Get());
                    if (!data.Result().IsPresent)
                        return DataResult<IntProvider>.Error(() => "failed to parse weighted_list data");
                    var weight = ReadInt(ops, map, "weight") ?? 1;
                    entries.Add(new Weighted<IntProvider>(data.GetOrThrow(), weight));
                }
                if (entries.Count == 0)
                    return DataResult<IntProvider>.Error(() => "weighted_list needs at least one element");
                return DataResult<IntProvider>.Success(new WeightedListInt(WeightedList<IntProvider>.Of(entries)));
            }
            default:
                return DataResult<IntProvider>.Error(() => $"unknown int provider type: {type}");
        }
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, IntProvider value)
    {
        //Constants encode as a bare int, keeping the same direction as vanilla either
        if (value is ConstantInt constant)
            return DataResult<U>.Success(ops.CreateInt(constant.Value));
        var builder = ops.MapBuilder();
        switch (value)
        {
            case UniformInt uniform:
                builder.Add("type", ops.CreateString("minecraft:uniform"));
                builder.Add("min_inclusive", ops.CreateInt(uniform.MinInclusive));
                builder.Add("max_inclusive", ops.CreateInt(uniform.MaxInclusive));
                break;
            case BiasedToBottomInt biased:
                builder.Add("type", ops.CreateString("minecraft:biased_to_bottom"));
                builder.Add("min_inclusive", ops.CreateInt(biased.MinInclusive));
                builder.Add("max_inclusive", ops.CreateInt(biased.MaxInclusive));
                break;
            case ClampedNormalInt normal:
                builder.Add("type", ops.CreateString("minecraft:clamped_normal"));
                builder.Add("mean", ops.CreateFloat(normal.Mean));
                builder.Add("deviation", ops.CreateFloat(normal.Deviation));
                builder.Add("min_inclusive", ops.CreateInt(normal.MinInclusive));
                builder.Add("max_inclusive", ops.CreateInt(normal.MaxInclusive));
                break;
            case TrapezoidInt trapezoid:
                builder.Add("type", ops.CreateString("minecraft:trapezoid"));
                builder.Add("min", ops.CreateInt(trapezoid.MinInclusive));
                builder.Add("max", ops.CreateInt(trapezoid.MaxInclusive));
                builder.Add("plateau", ops.CreateInt(trapezoid.Plateau));
                break;
            case ClampedInt clamped:
            {
                var source = EncodeStart(ops, clamped.Source);
                if (!source.Result().IsPresent) return source;
                builder.Add("type", ops.CreateString("minecraft:clamped"));
                builder.Add("source", source.GetOrThrow());
                builder.Add("min_inclusive", ops.CreateInt(clamped.MinInclusive));
                builder.Add("max_inclusive", ops.CreateInt(clamped.MaxInclusive));
                break;
            }
            default:
                return DataResult<U>.Error(() => $"encoding this int provider is not supported yet: {value.GetType().Name}");
        }
        return builder.Build(ops.Empty());
    }

    //ReadString reads a string field; missing or non-string returns null
    private static string? ReadString<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var tag = input.Get(name);
        if (!tag.IsPresent) return null;
        var text = ops.GetStringValue(tag.Get());
        return text.Result().IsPresent ? text.GetOrThrow() : null;
    }

    //ReadInt reads an int field; missing or non-number returns null
    private static int? ReadInt<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var tag = input.Get(name);
        if (!tag.IsPresent) return null;
        var value = ops.GetNumberValue(tag.Get());
        return value.Result().IsPresent ? (int)value.GetOrThrow() : null;
    }

    //ReadFloat reads a float field; missing or non-number returns null
    private static float? ReadFloat<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var tag = input.Get(name);
        if (!tag.IsPresent) return null;
        var value = ops.GetNumberValue(tag.Get());
        return value.Result().IsPresent ? (float)value.GetOrThrow() : null;
    }
}
