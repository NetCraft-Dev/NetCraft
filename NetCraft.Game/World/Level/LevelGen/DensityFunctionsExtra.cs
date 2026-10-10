using NetCraft.Game.Util;
using NetCraft.Game.World.Level.LevelGen.Synth;
using NetCraft.Util;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen;

//DensityFunctionsExtra extended density function set, maps to the remaining nested classes of vanilla DensityFunctions
//Spline/MarkerNode/RangeChoice/IntervalSelect/EndIslands/BlendAlpha/BlendOffset/Beardifier/FindTopSurface
//These classes are widely referenced when NoiseRouterData builds density trees and must match vanilla behaviour
public static class DensityFunctionsExtra
{
    //MarkerType cache marker type, maps to vanilla DensityFunctions.Marker.Type
    //Identifies a density function's caching strategy during chunk generation; Interpolated results are interpolated across cells, the rest control cache granularity
    public enum MarkerType
    {
        Interpolated,
        FlatCache,
        Cache2D,
        CacheOnce,
        CacheAllInCell,
        BlendDensity
    }

    //RangeChoice range choice factory, maps to vanilla DensityFunctions.rangeChoice
    public static RangeChoice RangeChoice(DensityFunction input, double minInclusive, double maxExclusive,
        DensityFunction whenInRange, DensityFunction whenOutOfRange)
        => new(input, minInclusive, maxExclusive, whenInRange, whenOutOfRange);

    //IntervalSelect multi-segment select factory, maps to vanilla DensityFunctions.intervalSelect
    public static IntervalSelect IntervalSelect(DensityFunction input, double[] thresholds, DensityFunction[] functions)
        => new(input, thresholds, functions);

    //Interpolated interpolation marker factory, maps to vanilla DensityFunctions.interpolated
    public static MarkerNode Interpolated(DensityFunction function) => new(MarkerType.Interpolated, function);

    //FlatCache flat cache marker factory, maps to vanilla DensityFunctions.flatCache
    public static MarkerNode FlatCache(DensityFunction function) => new(MarkerType.FlatCache, function);

    //Cache2D 2D cache marker factory, maps to vanilla DensityFunctions.cache2d
    public static MarkerNode Cache2D(DensityFunction function) => new(MarkerType.Cache2D, function);

    //CacheOnce single-shot cache marker factory, maps to vanilla DensityFunctions.cacheOnce
    public static MarkerNode CacheOnce(DensityFunction function) => new(MarkerType.CacheOnce, function);

    //CacheAllInCell whole-cell cache marker factory, maps to vanilla DensityFunctions.cacheAllInCell
    public static MarkerNode CacheAllInCell(DensityFunction function) => new(MarkerType.CacheAllInCell, function);

    //BlendDensity blend density marker factory, maps to vanilla DensityFunctions.blendDensity
    public static MarkerNode BlendDensity(DensityFunction function) => new(MarkerType.BlendDensity, function);

    //EndIslands End island density factory, maps to vanilla DensityFunctions.endIslands
    public static EndIslandDensityFunction EndIslands(long seed) => new(seed);

    //Spline spline density factory, maps to vanilla DensityFunctions.spline
    public static SplineFunction Spline(CubicSpline spline) => new(spline);
}

//MarkerNode cache marker node, maps to vanilla DensityFunctions.Marker
//Wraps a type and a wrapped child function; compute delegates to wrapped, and BlendDensity returns infinite min/max
public sealed class MarkerNode : DensityFunction
{
    public DensityFunctionsExtra.MarkerType Type { get; }
    public DensityFunction Wrapped { get; }

    public MarkerNode(DensityFunctionsExtra.MarkerType type, DensityFunction wrapped)
    {
        Type = type;
        Wrapped = wrapped;
    }

    public double Compute(FunctionContext context) => Wrapped.Compute(context);

    public void FillArray(double[] output, ContextProvider contextProvider)
        => Wrapped.FillArray(output, contextProvider);

    public DensityFunction MapChildren(Visitor visitor) => new MarkerNode(Type, visitor.Apply(Wrapped));

    public double MinValue => Type == DensityFunctionsExtra.MarkerType.BlendDensity
        ? double.NegativeInfinity : Wrapped.MinValue;

    public double MaxValue => Type == DensityFunctionsExtra.MarkerType.BlendDensity
        ? double.PositiveInfinity : Wrapped.MaxValue;
}

//SplineFunction spline density function, maps to vanilla DensityFunctions.Spline
//Wraps a CubicSpline as a DensityFunction; compute delegates to spline.Sample
//Simplified design holds the CubicSpline directly without Coordinate/Point intermediate layers, since CubicSpline already accepts a FunctionContext
public sealed class SplineFunction : DensityFunction
{
    private readonly CubicSpline _spline;

    public SplineFunction(CubicSpline spline) { _spline = spline; }

    public CubicSpline Spline => _spline;

    public double Compute(FunctionContext context) => _spline.Sample(context);

    public void FillArray(double[] output, ContextProvider contextProvider)
        => contextProvider.FillAllDirectly(output, this);

    public double MinValue => _spline.MinValue;
    public double MaxValue => _spline.MaxValue;

    //mapChildren delegates to spline.mapCoordinates, replacing inner DensityFunction coordinates with the visitor.apply result
    public DensityFunction MapChildren(Visitor visitor)
        => new SplineFunction(_spline.MapCoordinates(visitor.Apply));
}

//RangeChoice range choice density function, maps to vanilla DensityFunctions.RangeChoice
//Returns whenInRange when the input falls in [minInclusive, maxExclusive) and whenOutOfRange otherwise
public sealed class RangeChoice : DensityFunction
{
    public DensityFunction Input { get; }
    public double MinInclusive { get; }
    public double MaxExclusive { get; }
    public DensityFunction WhenInRange { get; }
    public DensityFunction WhenOutOfRange { get; }

    public RangeChoice(DensityFunction input, double minInclusive, double maxExclusive,
        DensityFunction whenInRange, DensityFunction whenOutOfRange)
    {
        Input = input;
        MinInclusive = minInclusive;
        MaxExclusive = maxExclusive;
        WhenInRange = whenInRange;
        WhenOutOfRange = whenOutOfRange;
    }

    public double Compute(FunctionContext context)
    {
        var v = Input.Compute(context);
        return v >= MinInclusive && v < MaxExclusive
            ? WhenInRange.Compute(context)
            : WhenOutOfRange.Compute(context);
    }

    public void FillArray(double[] output, ContextProvider contextProvider)
    {
        Input.FillArray(output, contextProvider);
        for (var i = 0; i < output.Length; i++)
        {
            var v = output[i];
            output[i] = v >= MinInclusive && v < MaxExclusive
                ? WhenInRange.Compute(contextProvider.ForIndex(i))
                : WhenOutOfRange.Compute(contextProvider.ForIndex(i));
        }
    }

    public DensityFunction MapChildren(Visitor visitor)
        => new RangeChoice(visitor.Apply(Input), MinInclusive, MaxExclusive,
            visitor.Apply(WhenInRange), visitor.Apply(WhenOutOfRange));

    public double MinValue => Math.Min(WhenInRange.MinValue, WhenOutOfRange.MinValue);
    public double MaxValue => Math.Max(WhenInRange.MaxValue, WhenOutOfRange.MaxValue);
}

//IntervalSelect multi-segment threshold select density function, maps to vanilla DensityFunctions.IntervalSelect
//The input selects a function by ascending thresholds; the threshold count must be one less than the function count
public sealed class IntervalSelect : DensityFunction
{
    public DensityFunction Input { get; }
    public double[] Thresholds { get; }
    public DensityFunction[] Functions { get; }

    public IntervalSelect(DensityFunction input, double[] thresholds, DensityFunction[] functions)
    {
        if (thresholds.Length != functions.Length - 1)
            throw new ArgumentException(
                $"Expected {functions.Length - 1} thresholds for {functions.Length} functions, but got {thresholds.Length}");
        for (var i = 1; i < thresholds.Length; i++)
            if (thresholds[i] < thresholds[i - 1])
                throw new ArgumentException("Threshold values must be ordered from smallest to largest");
        Input = input;
        Thresholds = thresholds;
        Functions = functions;
    }

    private double Compute(FunctionContext context, double input)
    {
        for (var i = 0; i < Thresholds.Length; i++)
            if (input < Thresholds[i])
                return Functions[i].Compute(context);
        return Functions[^1].Compute(context);
    }

    public double Compute(FunctionContext context) => Compute(context, Input.Compute(context));

    public void FillArray(double[] output, ContextProvider contextProvider)
    {
        Input.FillArray(output, contextProvider);
        for (var i = 0; i < output.Length; i++)
            output[i] = Compute(contextProvider.ForIndex(i), output[i]);
    }

    public DensityFunction MapChildren(Visitor visitor)
    {
        var newFunctions = Functions.Select(f => visitor.Apply(f)).ToArray();
        return new IntervalSelect(visitor.Apply(Input), Thresholds, newFunctions);
    }

    public double MinValue => Functions.Select(f => f.MinValue).Min();
    public double MaxValue => Functions.Select(f => f.MaxValue).Max();
}

//EndIslandDensityFunction End island density function, maps to vanilla DensityFunctions.EndIslandDensityFunction
//Uses SimplexNoise to generate the main island and outlying island density; compute returns (heightValue - 8) / 128
public sealed class EndIslandDensityFunction : SimpleFunction
{
    private const float IslandThreshold = -0.9f;

    //IslandChunkDistanceSqr minimum squared distance for End island generation, maps to vanilla NoiseRouterData.ISLAND_CHUNK_DISTANCE_SQR
    //Step 5 will reference the shared constant when NoiseRouterData is created; this local copy keeps step 3 self-contained
    private const long IslandChunkDistanceSqr = 4096L;

    private readonly SimplexNoise _islandNoise;

    public EndIslandDensityFunction(long seed)
    {
        var islandRandom = new LegacyRandomSource(seed);
        islandRandom.ConsumeCount(17292);
        _islandNoise = new SimplexNoise(islandRandom);
    }

    //GetHeightValue End height value, maps to vanilla getHeightValue
    //Starts from eight times the sectionX/Z distance and scans a 25x25 of surrounding chunks for the maximum island height to overlay
    private static float GetHeightValue(SimplexNoise islandNoise, int sectionX, int sectionZ)
    {
        var chunkX = sectionX / 2;
        var chunkZ = sectionZ / 2;
        var subSectionX = sectionX % 2;
        var subSectionZ = sectionZ % 2;
        var doffs = 100.0f - (Mth.Sqrt((float)(sectionX * sectionX + sectionZ * sectionZ)) * 8.0f);
        var doffs2 = Mth.Clamp(doffs, -100.0f, 80.0f);
        for (var xo = -12; xo <= 12; xo++)
        {
            for (var zo = -12; zo <= 12; zo++)
            {
                var totalChunkX = (long)chunkX + xo;
                var totalChunkZ = (long)chunkZ + zo;
                if (totalChunkX * totalChunkX + totalChunkZ * totalChunkZ > IslandChunkDistanceSqr
                    && islandNoise.GetValue(totalChunkX, totalChunkZ) < IslandThreshold)
                {
                    var islandSize = ((Math.Abs(totalChunkX) * 3439.0f) + (Math.Abs(totalChunkZ) * 147.0f)) % 13.0f + 9.0f;
                    var xd = subSectionX - (xo * 2);
                    var zd = subSectionZ - (zo * 2);
                    var newDoffs = 100.0f - (Mth.Sqrt((float)(xd * xd + zd * zd)) * islandSize);
                    doffs2 = Math.Max(doffs2, Mth.Clamp(newDoffs, -100.0f, 80.0f));
                }
            }
        }
        return doffs2;
    }

    public double Compute(FunctionContext context)
        => (GetHeightValue(_islandNoise, context.BlockX / 8, context.BlockZ / 8) - 8.0) / 128.0;

    public double MinValue => -0.84375;
    public double MaxValue => 0.5625;
}

//BlendAlpha blend alpha density function, maps to vanilla DensityFunctions.BlendAlpha
//Always returns 1.0, used as the biome transition weight
public sealed class BlendAlpha : SimpleFunction
{
    public static readonly BlendAlpha Instance = new();

    public double Compute(FunctionContext context) => 1.0;
    public double MinValue => 1.0;
    public double MaxValue => 1.0;
}

//BlendOffset blend offset density function, maps to vanilla DensityFunctions.BlendOffset
//Always returns 0.0, used as the biome transition height compensation
public sealed class BlendOffset : SimpleFunction
{
    public static readonly BlendOffset Instance = new();

    public double Compute(FunctionContext context) => 0.0;
    public double MinValue => 0.0;
    public double MaxValue => 0.0;
}

//BeardifierMarker beardifier marker, maps to vanilla DensityFunctions.BeardifierMarker
//Placeholder density function; the real beardifier is injected by the structure system, so this always returns 0.0
public sealed class BeardifierMarker : SimpleFunction
{
    public static readonly BeardifierMarker Instance = new();

    public double Compute(FunctionContext context) => 0.0;
    public double MinValue => 0.0;
    public double MaxValue => 0.0;
}

//FindTopSurface find-top-surface density function, maps to vanilla DensityFunctions.FindTopSurface
//Walks down from upperBound in cellHeight steps and takes the first Y with density > 0 as the surface height
public sealed class FindTopSurface : DensityFunction
{
    public DensityFunction Density { get; }
    public DensityFunction UpperBound { get; }
    public int LowerBound { get; }
    public int CellHeight { get; }

    public FindTopSurface(DensityFunction density, DensityFunction upperBound, int lowerBound, int cellHeight)
    {
        Density = density;
        UpperBound = upperBound;
        LowerBound = lowerBound;
        CellHeight = cellHeight;
    }

    //Reusable descent probe, one per thread; the descent below rewrites it every step so a nested caller cannot corrupt it
    [ThreadStatic] private static SinglePointContext? _topSurfaceProbe;

    public double Compute(FunctionContext context)
    {
        var topY = Mth.Floor(UpperBound.Compute(context) / CellHeight) * CellHeight;
        if (topY <= LowerBound)
            return LowerBound;
        var i = topY;
        //One instance for the whole descent; X and Z never change, only Y walks. The probe is rewritten before each use
        //and the call on the next line does not retain it, so the reuse cannot be clobbered
        var probe = _topSurfaceProbe ??= new SinglePointContext(0, 0, 0);
        while (true)
        {
            var blockY = i;
            if (blockY < LowerBound)
                return LowerBound;
            probe.Set(context.BlockX, blockY, context.BlockZ);
            if (Density.Compute(probe) <= 0.0)
                i = blockY - CellHeight;
            else
                return blockY;
        }
    }

    public void FillArray(double[] output, ContextProvider contextProvider)
        => contextProvider.FillAllDirectly(output, this);

    public DensityFunction MapChildren(Visitor visitor)
        => new FindTopSurface(visitor.Apply(Density), visitor.Apply(UpperBound), LowerBound, CellHeight);

    public double MinValue => LowerBound;
    public double MaxValue => Math.Max(LowerBound, UpperBound.MaxValue);
}
