using NetCraft.Codec;
using NetCraft.Logging;
using NetCraft.Registry;
using NetCraft.Util;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen;

//HeightProvider height provider system, maps to vanilla net.minecraft.world.level.levelgen.heightproviders
//HeightProvider.Codec is either an anchor object or dispatch by the type field
public abstract class HeightProvider
{
    //Codec either an anchor or an object with type, maps to vanilla CODEC
    public static readonly Codec<HeightProvider> Codec = new HeightProviderCodec();

    //Sample draws a height from the random source and context, maps to vanilla sample
    public abstract int Sample(RandomSource random, WorldGenerationContext context);
}

//ConstantHeight constant height provider, maps to vanilla ConstantHeight
public class ConstantHeight : HeightProvider
{
    //Zero zero-height singleton, maps to vanilla ZERO
    public static readonly ConstantHeight Zero = new(VerticalAnchor.Absolute(0));

    public VerticalAnchor Value { get; }

    private ConstantHeight(VerticalAnchor value) => Value = value;

    public static ConstantHeight Of(VerticalAnchor value) => new(value);

    public override int Sample(RandomSource random, WorldGenerationContext context) => Value.ResolveY(context);

    public override string ToString() => Value.ToString();
}

//UniformHeight uniform distribution height provider, maps to vanilla UniformHeight
public class UniformHeight : HeightProvider
{
    private readonly HashSet<long> _warnedFor = new();

    public VerticalAnchor MinInclusive { get; }

    public VerticalAnchor MaxInclusive { get; }

    private UniformHeight(VerticalAnchor minInclusive, VerticalAnchor maxInclusive)
    {
        MinInclusive = minInclusive;
        MaxInclusive = maxInclusive;
    }

    public static UniformHeight Of(VerticalAnchor minInclusive, VerticalAnchor maxInclusive)
        => new(minInclusive, maxInclusive);

    //Sample returns the lower bound on an empty range and warns once per min/max pair, maps to vanilla sample
    public override int Sample(RandomSource random, WorldGenerationContext context)
    {
        var min = MinInclusive.ResolveY(context);
        var max = MaxInclusive.ResolveY(context);
        if (min > max)
        {
            if (_warnedFor.Add(((long)min << 32) | (uint)max))
                Log.Warning($"Empty height range: {this}");
            return min;
        }
        return Mth.RandomBetweenInclusive(random, min, max);
    }

    public override string ToString() => $"[{MinInclusive}-{MaxInclusive}]";
}

//TrapezoidHeight trapezoid distribution height provider, maps to vanilla TrapezoidHeight
//The middle of the range is a plateau, so samples near the plateau are more likely
public class TrapezoidHeight : HeightProvider
{
    public VerticalAnchor MinInclusive { get; }
    public VerticalAnchor MaxInclusive { get; }
    public int Plateau { get; }

    private TrapezoidHeight(VerticalAnchor minInclusive, VerticalAnchor maxInclusive, int plateau)
    {
        MinInclusive = minInclusive;
        MaxInclusive = maxInclusive;
        Plateau = plateau;
    }

    public static TrapezoidHeight Of(VerticalAnchor minInclusive, VerticalAnchor maxInclusive, int plateau)
        => new(minInclusive, maxInclusive, plateau);

    public static TrapezoidHeight Of(VerticalAnchor minInclusive, VerticalAnchor maxInclusive)
        => new(minInclusive, maxInclusive, 0);

    //Sample degenerates to a uniform distribution when the plateau spans the whole range, maps to vanilla sample
    public override int Sample(RandomSource random, WorldGenerationContext context)
    {
        var min = MinInclusive.ResolveY(context);
        var max = MaxInclusive.ResolveY(context);
        if (min > max)
        {
            Log.Warning($"Empty height range: {this}");
            return min;
        }
        var range = max - min;
        if (Plateau >= range) return Mth.RandomBetweenInclusive(random, min, max);
        var plateauStart = (range - Plateau) / 2;
        var plateauEnd = range - plateauStart;
        return min + Mth.RandomBetweenInclusive(random, 0, plateauEnd)
            + Mth.RandomBetweenInclusive(random, 0, plateauStart);
    }

    public override string ToString() => Plateau == 0
        ? $"triangle ({MinInclusive}-{MaxInclusive})"
        : $"trapezoid({Plateau}) in [{MinInclusive}-{MaxInclusive}]";
}

//VeryBiasedToBottomHeight strongly bottom-biased height provider, maps to vanilla VeryBiasedToBottomHeight
//Draws an upper bound three times and narrows each time, biasing the result strongly toward the bottom of the range
public class VeryBiasedToBottomHeight : HeightProvider
{
    public VerticalAnchor MinInclusive { get; }
    public VerticalAnchor MaxInclusive { get; }
    public int Inner { get; }

    private VeryBiasedToBottomHeight(VerticalAnchor minInclusive, VerticalAnchor maxInclusive, int inner)
    {
        MinInclusive = minInclusive;
        MaxInclusive = maxInclusive;
        Inner = inner;
    }

    public static VeryBiasedToBottomHeight Of(VerticalAnchor minInclusive, VerticalAnchor maxInclusive, int inner)
        => new(minInclusive, maxInclusive, inner);

    //Sample falls back to the lower bound when the range cannot fit the inner inset, maps to vanilla sample
    public override int Sample(RandomSource random, WorldGenerationContext context)
    {
        var min = MinInclusive.ResolveY(context);
        var max = MaxInclusive.ResolveY(context);
        if (max - min - Inner + 1 <= 0)
        {
            Log.Warning($"Empty height range: {this}");
            return min;
        }
        var upperInclusive = Mth.RandomBetweenInclusive(random, min + Inner, max);
        var biasedUpperInclusive = Mth.RandomBetweenInclusive(random, min, upperInclusive - 1);
        return Mth.RandomBetweenInclusive(random, min, biasedUpperInclusive - 1 + Inner);
    }

    public override string ToString() => $"biased[{MinInclusive}-{MaxInclusive} inner: {Inner}]";
}

//HeightProviderCodec height provider codec
//A VerticalAnchor-form object decodes to ConstantHeight; the rest dispatch by the type field
internal sealed class HeightProviderCodec : ScalarCodec<HeightProvider>
{
    public override DataResult<HeightProvider> Parse<U>(DynamicOps<U> ops, U input)
    {
        var anchor = VerticalAnchor.Codec.Parse(ops, input);
        if (anchor.Result().IsPresent)
            return DataResult<HeightProvider>.Success(ConstantHeight.Of(anchor.GetOrThrow()));
        return ops.GetMap(input).FlatMap(map => DecodeProvider(ops, map));
    }

    private static DataResult<HeightProvider> DecodeProvider<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var type = ReadType(ops, input);
        if (!type.IsPresent)
            return DataResult<HeightProvider>.Error(() => "missing type field for HeightProvider");
        switch (PathOf(type.Get()))
        {
            case "constant":
                return ReadAnchor(ops, input, "value").Map(anchor => (HeightProvider)ConstantHeight.Of(anchor));
            case "uniform":
            {
                var bounds = ReadBounds(ops, input);
                if (!bounds.Result().IsPresent)
                    return DataResult<HeightProvider>.Error(() => Failure("bounds", bounds));
                var (min, max) = bounds.GetOrThrow();
                return DataResult<HeightProvider>.Success(UniformHeight.Of(min, max));
            }
            case "trapezoid":
            {
                var bounds = ReadBounds(ops, input);
                if (!bounds.Result().IsPresent)
                    return DataResult<HeightProvider>.Error(() => Failure("bounds", bounds));
                var (min, max) = bounds.GetOrThrow();
                return DataResult<HeightProvider>.Success(
                    TrapezoidHeight.Of(min, max, ReadOptionalInt(ops, input, "plateau", 0)));
            }
            case "very_biased_to_bottom":
            {
                var bounds = ReadBounds(ops, input);
                if (!bounds.Result().IsPresent)
                    return DataResult<HeightProvider>.Error(() => Failure("bounds", bounds));
                var (min, max) = bounds.GetOrThrow();
                return DataResult<HeightProvider>.Success(
                    VeryBiasedToBottomHeight.Of(min, max, ReadOptionalInt(ops, input, "inner", 1)));
            }
            default:
                return DataResult<HeightProvider>.Error(() => $"unknown height provider type: {type.Get()}");
        }
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, HeightProvider value)
    {
        //Constants encode as a bare anchor object, keeping the same direction as vanilla either
        if (value is ConstantHeight constant)
            return VerticalAnchor.Codec.EncodeStart(ops, constant.Value);
        var builder = ops.MapBuilder();
        switch (value)
        {
            case UniformHeight uniform:
                builder.Add("type", ops.CreateString(Identifier.WithDefaultNamespace("uniform").ToString()));
                builder.Add("min_inclusive", AnchorValue(ops, uniform.MinInclusive));
                builder.Add("max_inclusive", AnchorValue(ops, uniform.MaxInclusive));
                break;
            case TrapezoidHeight trapezoid:
                builder.Add("type", ops.CreateString(Identifier.WithDefaultNamespace("trapezoid").ToString()));
                builder.Add("min_inclusive", AnchorValue(ops, trapezoid.MinInclusive));
                builder.Add("max_inclusive", AnchorValue(ops, trapezoid.MaxInclusive));
                builder.Add("plateau", ops.CreateInt(trapezoid.Plateau));
                break;
            case VeryBiasedToBottomHeight biased:
                builder.Add("type",
                    ops.CreateString(Identifier.WithDefaultNamespace("very_biased_to_bottom").ToString()));
                builder.Add("min_inclusive", AnchorValue(ops, biased.MinInclusive));
                builder.Add("max_inclusive", AnchorValue(ops, biased.MaxInclusive));
                builder.Add("inner", ops.CreateInt(biased.Inner));
                break;
            default:
                return DataResult<U>.Error(() => $"unsupported HeightProvider: {value?.GetType().Name}");
        }
        return builder.Build(ops.Empty());
    }

    //AnchorValue encodes a single anchor, throwing on failure
    private static U AnchorValue<U>(DynamicOps<U> ops, VerticalAnchor anchor)
        => VerticalAnchor.Codec.EncodeStart(ops, anchor).GetOrThrow();

    //ReadAnchor reads an anchor field; missing or invalid both return an error
    private static DataResult<VerticalAnchor> ReadAnchor<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var tag = input.Get(name);
        return tag.IsPresent
            ? VerticalAnchor.Codec.Parse(ops, tag.Get())
            : DataResult<VerticalAnchor>.Error(() => $"missing field: {name}");
    }

    //ReadType reads the type string field; missing or non-string returns empty
    private static Optional<string> ReadType<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var tag = input.Get("type");
        if (!tag.IsPresent) return Optional<string>.Empty();
        var text = ops.GetStringValue(tag.Get());
        return text.Result().IsPresent ? Optional<string>.Of(text.GetOrThrow()) : Optional<string>.Empty();
    }

    //PathOf takes only the path segment of a namespaced type
    private static string PathOf(string type)
    {
        var id = Identifier.TryParse(type);
        return id is not null ? id.Value.Path : type;
    }

    //ReadBounds reads the min_inclusive and max_inclusive anchor fields together
    private static DataResult<(VerticalAnchor Min, VerticalAnchor Max)> ReadBounds<U>(
        DynamicOps<U> ops, MapLike<U> input)
    {
        var min = ReadAnchor(ops, input, "min_inclusive");
        if (!min.Result().IsPresent)
            return DataResult<(VerticalAnchor, VerticalAnchor)>.Error(() => Failure("min_inclusive", min));
        var max = ReadAnchor(ops, input, "max_inclusive");
        return max.Result().IsPresent
            ? DataResult<(VerticalAnchor, VerticalAnchor)>.Success((min.GetOrThrow(), max.GetOrThrow()))
            : DataResult<(VerticalAnchor, VerticalAnchor)>.Error(() => Failure("max_inclusive", max));
    }

    //ReadOptionalInt reads an optional integer scalar field, using the default when missing or non-numeric
    private static int ReadOptionalInt<U>(DynamicOps<U> ops, MapLike<U> input, string name, int defaultValue)
    {
        var tag = input.Get(name);
        if (!tag.IsPresent) return defaultValue;
        var value = ops.GetNumberValue(tag.Get());
        return value.Result().IsPresent ? (int)value.GetOrThrow() : defaultValue;
    }

    //Failure builds the decode-failure message for a field, including the inner cause
    private static string Failure<T>(string name, DataResult<T> result)
    {
        var cause = string.Empty;
        result.ResultOrPartial(msg => cause = msg);
        return $"failed to decode {name}: {cause}";
    }
}
