using NetCraft.Codec;
using NetCraft.Registry;
using NetCraft.Util;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen;

//FloatProvider float provider system, maps to vanilla net.minecraft.util.valueproviders
//FloatProviders.Codec is either a bare float or dispatch by the type field
public abstract class FloatProvider
{
    //Sample draws a float from the random source, maps to vanilla sample
    public abstract float Sample(RandomSource random);

    //Min lower bound, maps to vanilla min
    public abstract float Min { get; }

    //Max upper bound, maps to vanilla max
    public abstract float Max { get; }
}

//ConstantFloat constant float provider, maps to vanilla ConstantFloat
public sealed class ConstantFloat : FloatProvider
{
    //Zero constant-zero singleton, maps to vanilla ZERO
    public static readonly ConstantFloat Zero = new(0f);

    public float Value { get; }

    public ConstantFloat(float value) => Value = value;

    //Of reuses the singleton for zero, maps to vanilla of
    public static ConstantFloat Of(float value) => value == 0f ? Zero : new ConstantFloat(value);

    public override float Sample(RandomSource random) => Value;

    public override float Min => Value;

    public override float Max => Value;

    public override string ToString() => Value.ToString();
}

//UniformFloat uniform distribution float provider, maps to vanilla UniformFloat
public sealed class UniformFloat : FloatProvider
{
    private readonly float _min;
    private readonly float _max;

    public UniformFloat(float min, float max)
    {
        _min = min;
        _max = max;
    }

    //Of requires max to be strictly greater than min, maps to vanilla of
    public static UniformFloat Of(float min, float max)
        => max <= min ? throw new ArgumentException("Max must exceed min") : new UniformFloat(min, max);

    public override float Sample(RandomSource random) => Mth.RandomBetween(random, _min, _max);

    public override float Min => _min;

    public override float Max => _max;

    public override string ToString() => $"[{_min}-{_max}]";
}

//TrapezoidFloat trapezoid distribution float provider, maps to vanilla TrapezoidFloat
public sealed class TrapezoidFloat : FloatProvider
{
    private readonly float _min;
    private readonly float _max;
    private readonly float _plateau;

    public TrapezoidFloat(float min, float max, float plateau)
    {
        _min = min;
        _max = max;
        _plateau = plateau;
    }

    public float Plateau => _plateau;

    public static TrapezoidFloat Of(float min, float max, float plateau) => new(min, max, plateau);

    //Sample adds two independent draws, maps to vanilla sample; the call order must not change
    public override float Sample(RandomSource random)
    {
        var range = _max - _min;
        var plateauStart = (range - _plateau) / 2f;
        var plateauEnd = range - plateauStart;
        return _min + (random.NextFloat() * plateauEnd) + (random.NextFloat() * plateauStart);
    }

    public override float Min => _min;

    public override float Max => _max;

    public override string ToString() => $"trapezoid({_plateau}) in [{_min}-{_max}]";
}

//FloatProviders float provider codec entry point, maps to vanilla FloatProviders
public static class FloatProviders
{
    //Codec either a bare float or an object with type, maps to vanilla CODEC
    public static readonly Codec<FloatProvider> Codec = new FloatProviderCodec();
}

//FloatProviderCodec float provider codec
//A bare number decodes to ConstantFloat; an object dispatches by the type field
internal sealed class FloatProviderCodec : ScalarCodec<FloatProvider>
{
    public override DataResult<FloatProvider> Parse<U>(DynamicOps<U> ops, U input)
    {
        var number = ops.GetNumberValue(input);
        if (number.Result().IsPresent)
            return DataResult<FloatProvider>.Success(ConstantFloat.Of((float)number.GetOrThrow()));
        return ops.GetMap(input).FlatMap(map => DecodeProvider(ops, map));
    }

    private static DataResult<FloatProvider> DecodeProvider<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var type = ReadType(ops, input);
        if (!type.IsPresent)
            return DataResult<FloatProvider>.Error(() => "missing type field for FloatProvider");
        switch (PathOf(type.Get()))
        {
            case "constant":
            {
                var value = ReadFloat(ops, input, "value");
                return value is null
                    ? DataResult<FloatProvider>.Error(() => "constant float requires numeric value")
                    : DataResult<FloatProvider>.Success(ConstantFloat.Of(value.Value));
            }
            case "uniform":
            {
                var min = ReadFloat(ops, input, "min_inclusive");
                var max = ReadFloat(ops, input, "max_exclusive");
                if (min is null || max is null)
                    return DataResult<FloatProvider>.Error(() => "uniform float requires min_inclusive and max_exclusive");
                if (max <= min)
                    return DataResult<FloatProvider>.Error(() => $"Max must be larger than min, min: {min}, max: {max}");
                return DataResult<FloatProvider>.Success(new UniformFloat(min.Value, max.Value));
            }
            case "trapezoid":
            {
                var min = ReadFloat(ops, input, "min");
                var max = ReadFloat(ops, input, "max");
                var plateau = ReadFloat(ops, input, "plateau");
                if (min is null || max is null || plateau is null)
                    return DataResult<FloatProvider>.Error(() => "trapezoid float requires min/max/plateau");
                if (max < min)
                    return DataResult<FloatProvider>.Error(() => $"Max must be larger than min: [{min}, {max}]");
                if (plateau > max - min)
                    return DataResult<FloatProvider>.Error(() => $"Plateau can at most be the full span: [{min}, {max}]");
                return DataResult<FloatProvider>.Success(new TrapezoidFloat(min.Value, max.Value, plateau.Value));
            }
            default:
                return DataResult<FloatProvider>.Error(() => $"unknown float provider type: {type.Get()}");
        }
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, FloatProvider value)
    {
        //Constants encode as a bare float, keeping the same direction as vanilla either
        if (value is ConstantFloat constant)
            return DataResult<U>.Success(ops.CreateFloat(constant.Value));
        var builder = ops.MapBuilder();
        switch (value)
        {
            case UniformFloat uniform:
                builder.Add("type", ops.CreateString(Identifier.WithDefaultNamespace("uniform").ToString()));
                builder.Add("min_inclusive", ops.CreateFloat(uniform.Min));
                builder.Add("max_exclusive", ops.CreateFloat(uniform.Max));
                break;
            case TrapezoidFloat trapezoid:
                builder.Add("type", ops.CreateString(Identifier.WithDefaultNamespace("trapezoid").ToString()));
                builder.Add("min", ops.CreateFloat(trapezoid.Min));
                builder.Add("max", ops.CreateFloat(trapezoid.Max));
                builder.Add("plateau", ops.CreateFloat(trapezoid.Plateau));
                break;
            default:
                return DataResult<U>.Error(() => $"unsupported FloatProvider: {value?.GetType().Name}");
        }
        return builder.Build(ops.Empty());
    }

    //ReadType reads the type string field; missing or non-string returns empty
    private static Optional<string> ReadType<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var tag = input.Get("type");
        if (!tag.IsPresent) return Optional<string>.Empty();
        var text = ops.GetStringValue(tag.Get());
        return text.Result().IsPresent ? Optional<string>.Of(text.GetOrThrow()) : Optional<string>.Empty();
    }

    //ReadFloat reads a float field; missing or non-number returns null
    private static float? ReadFloat<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var tag = input.Get(name);
        if (!tag.IsPresent) return null;
        var value = ops.GetNumberValue(tag.Get());
        return value.Result().IsPresent ? (float)value.GetOrThrow() : null;
    }

    //PathOf takes only the path segment of a namespaced type
    private static string PathOf(string type)
    {
        var id = Identifier.TryParse(type);
        return id is not null ? id.Value.Path : type;
    }
}
