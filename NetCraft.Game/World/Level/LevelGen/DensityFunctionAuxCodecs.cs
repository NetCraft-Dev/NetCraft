using NetCraft.Codec;
using NetCraft.Game.Util;
using NetCraft.Game.World.Level.LevelGen.Synth;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen;

//DensityFunctionRefs density function and noise parameter reference resolution, maps to vanilla RegistryFileCodec
//String form goes through a registry lookup, inline form goes through the element codec
//Prefers the RegistryAccess carried by RegistryOps, falling back to the static BuiltInRegistries
public static class DensityFunctionRefs
{
    //Resolve looks up an element by Identifier in the RegistryOps registry or the built-in registry
    public static DataResult<T> Resolve<T, U>(DynamicOps<U> ops, ResourceKey<Registry<T>> key,
        Registry<T> builtin, Identifier id) where T : class
    {
        if (ops is RegistryOps<U> registryOps)
        {
            var registry = registryOps.GetRegistry(key);
            if (registry is not null)
                return Lookup(registry, key, id);
        }
        return Lookup(builtin, key, id);
    }

    //ResolveDensityFunction resolves a density function reference string
    public static DataResult<DensityFunction> ResolveDensityFunction<U>(DynamicOps<U> ops, Identifier id)
    {
        if (ops is RegistryOps<U> registryOps)
        {
            var registry = registryOps.GetRegistry(Registries.DENSITY_FUNCTION);
            if (registry is not null)
                return AsDensityFunction(registry.GetValue(id), id);
        }
        return AsDensityFunction(BuiltInRegistries.DENSITY_FUNCTION.GetValue(id), id);
    }

    private static DataResult<T> Lookup<T>(Registry<T> registry, ResourceKey<Registry<T>> key, Identifier id)
        where T : class
    {
        var value = registry.GetValue(id);
        return value is null
            ? DataResult<T>.Error(() => $"Can't find value: {id} in registry {key}")
            : DataResult<T>.Success(value);
    }

    private static DataResult<DensityFunction> AsDensityFunction(object? value, Identifier id)
        => value is DensityFunction df
            ? DataResult<DensityFunction>.Success(df)
            : DataResult<DensityFunction>.Error(() => $"Can't find density function: {id}");
}

//NoiseHolderCodec noise holder codec, maps to vanilla DensityFunction.NoiseHolder.CODEC
//Accepts a reference string "minecraft:temperature" or an inline object {"firstOctave":..,"amplitudes":[..]}
public sealed class NoiseHolderCodec : ScalarCodec<NoiseHolder>
{
    public static readonly NoiseHolderCodec Instance = new();

    public override DataResult<NoiseHolder> Parse<U>(DynamicOps<U> ops, U input)
    {
        var stringResult = ops.GetStringValue(input);
        if (stringResult.Result().IsPresent)
        {
            var text = stringResult.GetOrThrow();
            var id = Identifier.TryParse(text);
            if (id is null)
                return DataResult<NoiseHolder>.Error(() => $"Invalid noise identifier: {text}");
            return DensityFunctionRefs.Resolve(ops, Registries.NOISE, BuiltInRegistries.NOISE, id.Value)
                .Map(data => new NoiseHolder(data));
        }
        return NoiseParameters.Codec.Parse(ops, input).Map(data => new NoiseHolder(data));
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, NoiseHolder value)
    {
        var data = value.NoiseData;
        if (data is null)
            return DataResult<U>.Error(() => "NoiseHolder has no noise data");
        var id = BuiltInRegistries.NOISE.GetKey(data);
        return id is not null
            ? DataResult<U>.Success(ops.CreateString(id.Value.ToString()))
            : NoiseParameters.Codec.EncodeStart(ops, data);
    }
}

//CubicSplineCodec cubic spline codec, maps to vanilla CubicSpline.codec
//Number form is a constant spline; object form is {coordinate, points[{location,value,derivative}]}
//value can nest further splines; coordinate is a density function field that supports reference strings
public sealed class CubicSplineCodec : ScalarCodec<CubicSpline>
{
    public static readonly CubicSplineCodec Instance = new();

    public override DataResult<CubicSpline> Parse<U>(DynamicOps<U> ops, U input)
    {
        var numberResult = ops.GetNumberValue(input);
        if (numberResult.Result().IsPresent)
            return DataResult<CubicSpline>.Success(new CubicSplineConstant((float)numberResult.GetOrThrow()));
        return ops.GetMap(input).FlatMap(map => DecodeSpline(ops, map));
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, CubicSpline value)
    {
        if (value is CubicSplineConstant constant)
            return DataResult<U>.Success(ops.CreateFloat(constant.Value));
        if (value is not CubicSplineMultipoint multipoint)
            return DataResult<U>.Error(() => $"Unsupported CubicSpline type: {value.GetType().Name}");

        var builder = ops.MapBuilder();
        var coordinateResult = DensityFunctionCodec.Instance.EncodeStart(ops, multipoint.Coordinate);
        if (!coordinateResult.Result().IsPresent)
            return DataResult<U>.Error(() => "Failed to encode spline coordinate");
        builder.Add("coordinate", coordinateResult.GetOrThrow());

        var points = new List<U>();
        for (var i = 0; i < multipoint.Locations.Length; i++)
        {
            var pointBuilder = ops.MapBuilder();
            pointBuilder.Add("location", ops.CreateFloat(multipoint.Locations[i]));
            var valueResult = EncodeStart(ops, multipoint.Values[i]);
            if (!valueResult.Result().IsPresent)
                return DataResult<U>.Error(() => "Failed to encode spline point value");
            pointBuilder.Add("value", valueResult.GetOrThrow());
            pointBuilder.Add("derivative", ops.CreateFloat(multipoint.Derivatives[i]));
            points.Add(pointBuilder.Build(ops.Empty()).GetOrThrow());
        }
        builder.Add("points", ops.CreateList(points));
        return builder.Build(ops.Empty());
    }

    //DecodeSpline parses the {coordinate, points} structure; location must be strictly increasing
    private static DataResult<CubicSpline> DecodeSpline<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var coordinateTag = input.Get("coordinate");
        if (!coordinateTag.IsPresent)
            return DataResult<CubicSpline>.Error(() => "Missing key coordinate");
        var coordinateResult = DensityFunctionCodec.Instance.Parse(ops, coordinateTag.Get());
        if (!coordinateResult.Result().IsPresent)
            return DataResult<CubicSpline>.Error(() => "Failed to decode spline coordinate");

        var pointsTag = input.Get("points");
        if (!pointsTag.IsPresent)
            return DataResult<CubicSpline>.Error(() => "Missing key points");
        var streamResult = ops.GetStream(pointsTag.Get());
        if (!streamResult.Result().IsPresent)
            return DataResult<CubicSpline>.Error(() => "spline points must be a list");

        var builder = new CubicSplineBuilder(coordinateResult.GetOrThrow(), v => v);
        var previousLocation = float.NegativeInfinity;
        var count = 0;
        foreach (var point in streamResult.GetOrThrow())
        {
            var mapResult = ops.GetMap(point);
            if (!mapResult.Result().IsPresent)
                return DataResult<CubicSpline>.Error(() => "spline point must be a map");
            var pointMap = mapResult.GetOrThrow();
            var locationTag = pointMap.Get("location");
            var valueTag = pointMap.Get("value");
            if (!locationTag.IsPresent || !valueTag.IsPresent)
                return DataResult<CubicSpline>.Error(() => "spline point requires location and value");
            var locationResult = ops.GetNumberValue(locationTag.Get());
            if (!locationResult.Result().IsPresent)
                return DataResult<CubicSpline>.Error(() => "spline location must be a number");
            var location = (float)locationResult.GetOrThrow();
            if (location <= previousLocation)
                return DataResult<CubicSpline>.Error(() => "Please register points in ascending order");
            previousLocation = location;

            var valueResult = Instance.Parse(ops, valueTag.Get());
            if (!valueResult.Result().IsPresent)
                return DataResult<CubicSpline>.Error(() => "Failed to decode spline point value");

            var derivative = 0.0f;
            var derivativeTag = pointMap.Get("derivative");
            if (derivativeTag.IsPresent)
            {
                var derivativeResult = ops.GetNumberValue(derivativeTag.Get());
                if (!derivativeResult.Result().IsPresent)
                    return DataResult<CubicSpline>.Error(() => "spline derivative must be a number");
                derivative = (float)derivativeResult.GetOrThrow();
            }
            builder.AddPoint(location, valueResult.GetOrThrow(), derivative);
            count++;
        }
        if (count == 0)
            return DataResult<CubicSpline>.Error(() => "Cannot create a multipoint spline with no points");
        return DataResult<CubicSpline>.Success(builder.Build());
    }
}
