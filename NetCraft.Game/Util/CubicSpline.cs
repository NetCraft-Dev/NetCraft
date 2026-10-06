using NetCraft.Util;
using NetCraft.Game.World.Level.LevelGen;

namespace NetCraft.Game.Util;

//CubicSpline cubic spline interpolation, maps to vanilla net.minecraft.util.CubicSpline<I>
//Vanilla uses a generic I extends BoundedFloatFunction<?>; simplified here to hold DensityFunction directly as the coordinate
//TerrainProvider builds the hierarchical spline tree with the builder pattern; each node's Sample reads the coordinate from FunctionContext
//Hermite cubic interpolation within an interval; endpoints are extrapolated with linearExtend to align with vanilla Multipoint.sample
public interface CubicSpline
{
    float Sample(FunctionContext context);
    float MinValue { get; }
    float MaxValue { get; }
    CubicSpline MapCoordinates(Func<DensityFunction, DensityFunction> mapper);

    //Constant factory, maps to vanilla CubicSpline.constant
    static CubicSpline Constant(float value) => new CubicSplineConstant(value);

    //Builder factory, maps to vanilla CubicSpline.builder(coordinate)
    static CubicSplineBuilder Builder(DensityFunction coordinate)
        => new(coordinate, v => v);

    //Builder factory with valueTransformer, maps to vanilla CubicSpline.builder(coordinate, valueTransformer)
    //valueTransformer transforms the value passed to addPoint; amplified mode uses it to scale the offset
    static CubicSplineBuilder Builder(DensityFunction coordinate, Func<float, float> valueTransformer)
        => new(coordinate, valueTransformer);
}

//Multipoint multi-point spline, maps to vanilla CubicSpline.Multipoint
//Holds coordinate/locations/values/derivatives; computes the min/max bounds on construction
public sealed class CubicSplineMultipoint : CubicSpline
{
    private readonly DensityFunction _coordinate;
    private readonly float[] _locations;
    private readonly IReadOnlyList<CubicSpline> _values;
    private readonly float[] _derivatives;
    private readonly float _minValue;
    private readonly float _maxValue;

    public CubicSplineMultipoint(DensityFunction coordinate, float[] locations, IReadOnlyList<CubicSpline> values, float[] derivatives)
    {
        ValidateSizes(locations, values, derivatives);
        _coordinate = coordinate;
        _locations = locations;
        _values = values;
        _derivatives = derivatives;

        var lastIndex = locations.Length - 1;
        var minValue = float.PositiveInfinity;
        var maxValue = float.NegativeInfinity;
        var minInput = (float)coordinate.MinValue;
        var maxInput = (float)coordinate.MaxValue;

        if (minInput < locations[0])
        {
            var edge1 = LinearExtend(minInput, locations, values[0].MinValue, derivatives, 0);
            var edge2 = LinearExtend(minInput, locations, values[0].MaxValue, derivatives, 0);
            minValue = Math.Min(float.PositiveInfinity, Math.Min(edge1, edge2));
            maxValue = Math.Max(float.NegativeInfinity, Math.Max(edge1, edge2));
        }
        if (maxInput > locations[lastIndex])
        {
            var edge1 = LinearExtend(maxInput, locations, values[lastIndex].MinValue, derivatives, lastIndex);
            var edge2 = LinearExtend(maxInput, locations, values[lastIndex].MaxValue, derivatives, lastIndex);
            minValue = Math.Min(minValue, Math.Min(edge1, edge2));
            maxValue = Math.Max(maxValue, Math.Max(edge1, edge2));
        }
        foreach (var value in values)
        {
            minValue = Math.Min(minValue, value.MinValue);
            maxValue = Math.Max(maxValue, value.MaxValue);
        }
        //Within the interval, when d1/d2 are non-zero, accounts for the Hermite cubic extremum bound to align with vanilla
        for (var i = 0; i < lastIndex; i++)
        {
            var x1 = locations[i];
            var x2 = locations[i + 1];
            var xDiff = x2 - x1;
            var v1 = values[i];
            var v2 = values[i + 1];
            var min1 = v1.MinValue;
            var max1 = v1.MaxValue;
            var min2 = v2.MinValue;
            var max2 = v2.MaxValue;
            var d1 = derivatives[i];
            var d2 = derivatives[i + 1];
            if (d1 != 0.0f || d2 != 0.0f)
            {
                var p1 = d1 * xDiff;
                var p2 = d2 * xDiff;
                var minLerp1 = Math.Min(min1, min2);
                var maxLerp1 = Math.Max(max1, max2);
                var minA = (p1 - max2) + min1;
                var maxA = (p1 - min2) + max1;
                var minB = (-p2 + min2) - max1;
                var maxB = (-p2 + max2) - min1;
                var minLerp2 = Math.Min(minA, minB);
                var maxLerp2 = Math.Max(maxA, maxB);
                minValue = Math.Min(minValue, minLerp1 + 0.25f * minLerp2);
                maxValue = Math.Max(maxValue, maxLerp1 + 0.25f * maxLerp2);
            }
        }
        _minValue = minValue;
        _maxValue = maxValue;
    }

    public float Sample(FunctionContext context)
    {
        var input = (float)_coordinate.Compute(context);
        var start = FindIntervalStart(_locations, input);
        var lastIndex = _locations.Length - 1;
        if (start < 0)
            return LinearExtend(input, _locations, _values[0].Sample(context), _derivatives, 0);
        if (start == lastIndex)
            return LinearExtend(input, _locations, _values[lastIndex].Sample(context), _derivatives, lastIndex);
        var x1 = _locations[start];
        var x2 = _locations[start + 1];
        var t = (input - x1) / (x2 - x1);
        var f1 = _values[start];
        var f2 = _values[start + 1];
        var d1 = _derivatives[start];
        var d2 = _derivatives[start + 1];
        var y1 = f1.Sample(context);
        var y2 = f2.Sample(context);
        var a = d1 * (x2 - x1) - (y2 - y1);
        var b = -d2 * (x2 - x1) + (y2 - y1);
        var offset = Mth.Lerp(t, y1, y2) + t * (1.0f - t) * Mth.Lerp(t, a, b);
        return offset;
    }

    public float MinValue => _minValue;
    public float MaxValue => _maxValue;

    //Coordinate/Points expose node data for Codec serialization
    public DensityFunction Coordinate => _coordinate;
    public float[] Locations => _locations;
    public IReadOnlyList<CubicSpline> Values => _values;
    public float[] Derivatives => _derivatives;

    public CubicSpline MapCoordinates(Func<DensityFunction, DensityFunction> mapper)
    {
        var newCoordinate = mapper(_coordinate);
        var newValues = _values.Select(v => v.MapCoordinates(mapper)).ToList();
        return new CubicSplineMultipoint(newCoordinate, _locations, newValues, _derivatives);
    }

    //LinearExtend endpoint linear extrapolation, maps to vanilla Multipoint.linearExtend
    //Returns the value itself when derivative is 0, otherwise extrapolates along the slope
    private static float LinearExtend(float input, float[] locations, float value, float[] derivatives, int index)
    {
        var derivative = derivatives[index];
        if (derivative == 0.0f)
            return value;
        return value + derivative * (input - locations[index]);
    }

    //FindIntervalStart binary-searches the start of the interval containing input, maps to vanilla findIntervalStart
    //Returns -1 when input is smaller than all locations; returns lastIndex when input is greater than or equal to the last one
    //Vanilla uses a lambda, but Java lambdas inline while a .NET Func<int,bool> allocates a capturing closure on every call
    //The binary search would also make a delegate call every round; pure overhead on the per-cell sampling path, so it is unrolled into a loop here
    private static int FindIntervalStart(float[] locations, float input)
    {
        var from = 0;
        var count = locations.Length;
        while (count > 0)
        {
            var half = count / 2;
            var middle = from + half;
            if (input < locations[middle])
            {
                count = half;
            }
            else
            {
                from = middle + 1;
                count -= half + 1;
            }
        }
        return from - 1;
    }

    private static void ValidateSizes(float[] locations, IReadOnlyList<CubicSpline> values, float[] derivatives)
    {
        if (locations.Length != values.Count || locations.Length != derivatives.Length)
            throw new ArgumentException($"All lengths must be equal, got: {locations.Length} {values.Count} {derivatives.Length}");
        if (locations.Length == 0)
            throw new ArgumentException("Cannot create a multipoint spline with no points");
    }
}

//CubicSplineConstant constant spline, maps to vanilla CubicSpline.Constant
//Every coordinate returns the fixed value; min/max equal value
public sealed class CubicSplineConstant : CubicSpline
{
    private readonly float _value;
    public CubicSplineConstant(float value) { _value = value; }
    public float Sample(FunctionContext context) => _value;
    public float MinValue => _value;
    public float MaxValue => _value;

    //Value exposes the constant value for Codec serialization
    public float Value => _value;

    public CubicSpline MapCoordinates(Func<DensityFunction, DensityFunction> mapper) => this;
}

//CubicSplineBuilder spline builder, maps to vanilla CubicSpline.Builder
//addPoint must be registered in ascending order; build constructs the Multipoint
public sealed class CubicSplineBuilder
{
    private readonly DensityFunction _coordinate;
    private readonly Func<float, float> _valueTransformer;
    private readonly List<float> _locations = new();
    private readonly List<CubicSpline> _values = new();
    private readonly List<float> _derivatives = new();

    public CubicSplineBuilder(DensityFunction coordinate, Func<float, float> valueTransformer)
    {
        _coordinate = coordinate;
        _valueTransformer = valueTransformer;
    }

    public CubicSplineBuilder AddPoint(float location, float value)
        => AddPoint(location, new CubicSplineConstant(_valueTransformer(value)), 0.0f);

    public CubicSplineBuilder AddPoint(float location, float value, float derivative)
        => AddPoint(location, new CubicSplineConstant(_valueTransformer(value)), derivative);

    public CubicSplineBuilder AddPoint(float location, CubicSpline sampler)
        => AddPoint(location, sampler, 0.0f);

    //AddPoint overload with a slope, for Codec to restore the derivative from JSON
    public CubicSplineBuilder AddPoint(float location, CubicSpline sampler, float derivative)
        => AddPointInternal(location, sampler, derivative);

    private CubicSplineBuilder AddPointInternal(float location, CubicSpline sampler, float derivative)
    {
        if (_locations.Count > 0 && location <= _locations[^1])
            throw new ArgumentException("Please register points in ascending order");
        _locations.Add(location);
        _values.Add(sampler);
        _derivatives.Add(derivative);
        return this;
    }

    public CubicSpline Build()
    {
        if (_locations.Count == 0)
            throw new InvalidOperationException("No elements added");
        return new CubicSplineMultipoint(_coordinate, _locations.ToArray(), _values.ToList(), _derivatives.ToArray());
    }
}
