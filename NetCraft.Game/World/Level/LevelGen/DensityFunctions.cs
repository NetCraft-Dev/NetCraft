using NetCraft.Codec;
using NetCraft.Game.Util;
using NetCraft.Game.World.Level.LevelGen.Synth;

namespace NetCraft.Game.World.Level.LevelGen;

//DensityFunctions density function collection, maps to vanilla net.minecraft.world.level.levelgen.DensityFunctions
//Central place for concrete DensityFunction implementations such as constant/noise/transform/clamp/mul-or-add/binary operations
//Codecs register into the DENSITY_FUNCTION_TYPE registry, to be wired up once the dispatch codec subsystem is ready
public static class DensityFunctions
{
    //Constant.ConstantValue codec serialising a bare double, aligned with vanilla Constant.CODEC
    public static readonly Codec<Constant> ConstantCodec =
        Codecs.Double.ComapFlatMap(
            v => DataResult<Constant>.Success(new Constant(v)),
            c => c.Value);

    //YClampedGradientCodec simplified to constant fields, aligned with vanilla YClampedGradient.CODEC
    public static readonly Codec<YClampedGradient> YClampedGradientCodec =
        RecordCodecBuilder.Of4(
            Codecs.Int.FieldOf("from_y").ForGetter<YClampedGradient, int>(g => g.FromY),
            Codecs.Int.FieldOf("to_y").ForGetter<YClampedGradient, int>(g => g.ToY),
            Codecs.Double.FieldOf("from_value").ForGetter<YClampedGradient, double>(g => g.FromValue),
            Codecs.Double.FieldOf("to_value").ForGetter<YClampedGradient, double>(g => g.ToValue),
            (fromY, toY, fromValue, toValue) => new YClampedGradient(fromY, toY, fromValue, toValue));

    //Marker marker interface, maps to vanilla DensityFunctions.Marker
    //Marks leaf density functions with no children; only mapChildren is overridden to return itself, not mapAll or subtrees would go unvisited
    public interface Marker : DensityFunction
    {
        //MapChildren has no children so it returns itself
        DensityFunction DensityFunction.MapChildren(Visitor visitor) => this;
    }

    //Factory method collection, maps to the vanilla DensityFunctions static factories
    //NoiseRouterData uses these to build the overworld/nether/end density trees

    //Zero zero constant, maps to vanilla zero
    public static DensityFunction Zero() => NetCraft.Game.World.Level.LevelGen.Constant.Zero;

    //ConstantValue constant, maps to vanilla constant
    //The name avoids clashing with the Constant class name; in C# a method group takes priority over a type of the same name and would fail to compile
    public static DensityFunction ConstantValue(double value) => new Constant(value);

    //YClampedGradient Y-axis clamped gradient, maps to vanilla yClampedGradient
    public static DensityFunction YClampedGradient(int fromY, int toY, double fromValue, double toValue)
        => new YClampedGradient(fromY, toY, fromValue, toValue);

    //Add binary add, maps to vanilla add; folds to MulOrAdd when a Constant is involved
    public static DensityFunction Add(DensityFunction a, DensityFunction b)
        => TwoArgumentCreate(Ap2.OpType.Add, a, b);

    //Mul binary multiply, maps to vanilla mul; folds to MulOrAdd when a Constant is involved
    public static DensityFunction Mul(DensityFunction a, DensityFunction b)
        => TwoArgumentCreate(Ap2.OpType.Mul, a, b);

    //Min binary minimum, maps to vanilla min
    public static DensityFunction Min(DensityFunction a, DensityFunction b)
        => new Ap2(Ap2.OpType.Min, a, b);

    //Max binary maximum, maps to vanilla max
    public static DensityFunction Max(DensityFunction a, DensityFunction b)
        => new Ap2(Ap2.OpType.Max, a, b);

    //TwoArgumentCreate binary operation factory, maps to vanilla TwoArgumentSimpleFunction.create
    //For Add/Mul, folds to MulOrAdd when either argument is a Constant, avoiding pointless nesting
    private static DensityFunction TwoArgumentCreate(Ap2.OpType type, DensityFunction a, DensityFunction b)
    {
        if (type is Ap2.OpType.Add or Ap2.OpType.Mul)
        {
            if (a is Constant ca)
                return MulOrAdd.Of(
                    type == Ap2.OpType.Add ? MulOrAdd.OpType.Add : MulOrAdd.OpType.Mul,
                    ca.Value, b);
            if (b is Constant cb)
                return MulOrAdd.Of(
                    type == Ap2.OpType.Add ? MulOrAdd.OpType.Add : MulOrAdd.OpType.Mul,
                    cb.Value, a);
        }
        return new Ap2(type, a, b);
    }

    //Lerp three-argument linear interpolation, maps to vanilla lerp(alpha, first, second)
    //Takes the simplified path when first is a Constant; otherwise cacheOnce caches alpha to avoid repeated sampling
    public static DensityFunction Lerp(DensityFunction alpha, DensityFunction first, DensityFunction second)
    {
        if (first is Constant c)
            return Lerp(alpha, c.Value, second);
        var cached = CacheOnce(alpha);
        var oneMinus = Add(Mul(cached, ConstantValue(-1.0)), ConstantValue(1.0));
        return Add(Mul(first, oneMinus), Mul(second, cached));
    }

    //Lerp constant form, maps to vanilla lerp(factor, firstValue, second)
    //Returns mul(factor, second - firstValue) + firstValue
    public static DensityFunction Lerp(DensityFunction factor, double first, DensityFunction second)
        => Add(Mul(factor, Add(second, ConstantValue(-first))), ConstantValue(first));

    //Clamp clamping, maps to vanilla clamp
    public static DensityFunction Clamp(DensityFunction input, double min, double max)
        => new Clamp(input, min, max);

    //Interpolated interpolation marker, maps to vanilla interpolated
    public static DensityFunction Interpolated(DensityFunction function)
        => DensityFunctionsExtra.Interpolated(function);

    //FlatCache flat cache, maps to vanilla flatCache
    public static DensityFunction FlatCache(DensityFunction function)
        => DensityFunctionsExtra.FlatCache(function);

    //Cache2D 2D cache, maps to vanilla cache2d
    public static DensityFunction Cache2D(DensityFunction function)
        => DensityFunctionsExtra.Cache2D(function);

    //CacheOnce single-shot cache, maps to vanilla cacheOnce
    public static DensityFunction CacheOnce(DensityFunction function)
        => DensityFunctionsExtra.CacheOnce(function);

    //BlendDensity blend density marker, maps to vanilla blendDensity
    public static DensityFunction BlendDensity(DensityFunction function)
        => DensityFunctionsExtra.BlendDensity(function);

    //Spline spline density function, maps to vanilla spline
    public static DensityFunction Spline(CubicSpline spline)
        => DensityFunctionsExtra.Spline(spline);

    //Noise noise density function, maps to vanilla noise(holder)
    public static DensityFunction Noise(NoiseParameters noiseData)
        => Noise(noiseData, 1.0, 1.0);

    //Noise scaled noise, maps to vanilla noise(holder, xzScale, yScale)
    public static DensityFunction Noise(NoiseParameters noiseData, double xzScale, double yScale)
        => new Noise(new NoiseHolder(noiseData), xzScale, yScale);

    //Noise Y-scaled form, maps to vanilla noise(holder, yScale), simplified with xzScale=1
    public static DensityFunction Noise(NoiseParameters noiseData, double yScale)
        => Noise(noiseData, 1.0, yScale);

    //MappedNoise unit-range mapped noise, maps to vanilla mappedNoise(holder, xzScale, yScale, minTarget, maxTarget)
    //Maps the noise range [-1,1] onto [minTarget, maxTarget]
    public static DensityFunction MappedNoise(NoiseParameters noiseData, double xzScale, double yScale, double minTarget, double maxTarget)
        => MapFromUnitTo(Noise(noiseData, xzScale, yScale), minTarget, maxTarget);

    //MappedNoise simplified form, maps to vanilla mappedNoise(holder, yScale, minTarget, maxTarget)
    public static DensityFunction MappedNoise(NoiseParameters noiseData, double yScale, double minTarget, double maxTarget)
        => MappedNoise(noiseData, 1.0, yScale, minTarget, maxTarget);

    //MappedNoise both scales one, maps to vanilla mappedNoise(holder, minTarget, maxTarget)
    public static DensityFunction MappedNoise(NoiseParameters noiseData, double minTarget, double maxTarget)
        => MappedNoise(noiseData, 1.0, 1.0, minTarget, maxTarget);

    //MapFromUnitTo unit-range mapping, maps to vanilla mapFromUnitTo
    //middle = (min+max)/2, factor = (max-min)/2, output = middle + factor * input
    private static DensityFunction MapFromUnitTo(DensityFunction function, double min, double max)
    {
        var middle = (min + max) * 0.5;
        var factor = (max - min) * 0.5;
        return Add(ConstantValue(middle), Mul(ConstantValue(factor), function));
    }

    //RangeChoice range choice, maps to vanilla rangeChoice
    public static DensityFunction RangeChoice(DensityFunction input, double minInclusive, double maxExclusive,
        DensityFunction whenInRange, DensityFunction whenOutOfRange)
        => DensityFunctionsExtra.RangeChoice(input, minInclusive, maxExclusive, whenInRange, whenOutOfRange);

    //IntervalSelect multi-segment select, maps to vanilla intervalSelect
    public static DensityFunction IntervalSelect(DensityFunction input, double[] thresholds, DensityFunction[] functions)
        => DensityFunctionsExtra.IntervalSelect(input, thresholds, functions);

    //ShiftA/ShiftB/Shift noise shift, maps to vanilla shiftA/shiftB/shift
    public static DensityFunction ShiftA(NoiseParameters noiseData) => new ShiftA(new NoiseHolder(noiseData));
    public static DensityFunction ShiftB(NoiseParameters noiseData) => new ShiftB(new NoiseHolder(noiseData));
    public static DensityFunction Shift(NoiseParameters noiseData) => new Shift(new NoiseHolder(noiseData));

    //ShiftedNoise2d 2D shifted noise, maps to vanilla shiftedNoise2d
    //shiftY uses Constant.Zero, the vanilla zero() placeholder
    public static DensityFunction ShiftedNoise2d(DensityFunction shiftX, DensityFunction shiftZ, double xzScale, NoiseParameters noiseData)
        => new ShiftedNoise(new NoiseHolder(noiseData), xzScale, 0.0, shiftX, Zero(), shiftZ);

    //EndIslands End island density, maps to vanilla endIslands
    public static DensityFunction EndIslands(long seed) => DensityFunctionsExtra.EndIslands(seed);

    //FindTopSurface finds the top surface, maps to vanilla findTopSurface
    public static DensityFunction FindTopSurface(DensityFunction density, DensityFunction upperBound, int lowerBound, int stepSize)
        => new FindTopSurface(density, upperBound, lowerBound, stepSize);

    //BlendAlpha blend alpha singleton, maps to vanilla blendAlpha
    public static DensityFunction BlendAlpha() => NetCraft.Game.World.Level.LevelGen.BlendAlpha.Instance;

    //BlendOffset blend offset singleton, maps to vanilla blendOffset
    public static DensityFunction BlendOffset() => NetCraft.Game.World.Level.LevelGen.BlendOffset.Instance;

    //Abs unary absolute value, maps to vanilla map(input, ABS)
    public static DensityFunction Abs(DensityFunction input) => new MappedTypes.Abs(input);

    //Square unary square, maps to vanilla map(input, SQUARE)
    public static DensityFunction Square(DensityFunction input) => new MappedTypes.Square(input);

    //Cube unary cube, maps to vanilla map(input, CUBE)
    public static DensityFunction Cube(DensityFunction input) => new MappedTypes.Cube(input);

    //HalfNegative unary halve-negative, maps to vanilla map(input, HALF_NEGATIVE)
    public static DensityFunction HalfNegative(DensityFunction input) => new MappedTypes.HalfNegative(input);

    //QuarterNegative unary quarter-negative, maps to vanilla map(input, QUARTER_NEGATIVE)
    public static DensityFunction QuarterNegative(DensityFunction input) => new MappedTypes.QuarterNegative(input);

    //Invert unary reciprocal, maps to vanilla map(input, INVERT)
    public static DensityFunction Invert(DensityFunction input) => new MappedTypes.Invert(input);

    //Squeeze unary squeeze, maps to vanilla map(input, SQUEEZE)
    public static DensityFunction Squeeze(DensityFunction input) => new MappedTypes.Squeeze(input);
}

//Constant constant density function, maps to vanilla DensityFunctions.Constant
//Returns a fixed value at every coordinate, used as an offset/scale baseline
public sealed class Constant : DensityFunctions.Marker
{
    public double Value { get; }

    public Constant(double value) { Value = value; }

    public double Compute(FunctionContext context) => Value;

    public void FillArray(double[] output, ContextProvider contextProvider)
        => contextProvider.FillAllDirectly(output, this);

    public double MinValue => Value;
    public double MaxValue => Value;

    //Zero and One predefined constants, maps to vanilla ConstantZero/ConstantOne
    public static readonly Constant Zero = new(0.0);
    public static readonly Constant One = new(1.0);
}

//Noise noise density function, maps to vanilla DensityFunctions.Noise
//Wraps a NoiseHolder and samples a noise value by coordinate; not a leaf, so the visitor must be able to reach the noise reference
public sealed class Noise : DensityFunction
{
    public NoiseHolder NoiseData { get; }
    public double XzScale { get; }
    public double YScale { get; }

    public Noise(NoiseHolder noise, double xzScale, double yScale)
    {
        NoiseData = noise;
        XzScale = xzScale;
        YScale = yScale;
    }

    public Noise(NoiseHolder noise) : this(noise, 1.0, 1.0) { }

    public double Compute(FunctionContext context)
    {
        var x = context.BlockX * XzScale;
        var y = context.BlockY * YScale;
        var z = context.BlockZ * XzScale;
        return NoiseData.GetValue(x, y, z);
    }

    public void FillArray(double[] output, ContextProvider contextProvider)
    {
        for (var i = 0; i < output.Length; i++)
            output[i] = Compute(contextProvider.ForIndex(i));
    }

    //MapChildren replaces the noise reference, maps to visitNoise inside vanilla mapChildren
    public DensityFunction MapChildren(Visitor visitor)
        => new Noise(visitor.VisitNoise(NoiseData), XzScale, YScale);

    public double MinValue => -NoiseData.MaxValue;
    public double MaxValue => NoiseData.MaxValue;
}

//Mapped base class for mapped density functions, maps to vanilla DensityFunctions.Mapped
//Holds the input child function; subclasses transform compute/min/max as needed
public abstract class Mapped : DensityFunction
{
    public DensityFunction Input { get; }

    protected Mapped(DensityFunction input) { Input = input; }

    public abstract double Compute(FunctionContext context);

    public void FillArray(double[] output, ContextProvider contextProvider)
    {
        for (var i = 0; i < output.Length; i++)
            output[i] = Compute(contextProvider.ForIndex(i));
    }

    public abstract DensityFunction MapChildren(Visitor visitor);

    public abstract double MinValue { get; }
    public abstract double MaxValue { get; }
}

//Clamp clamp density function, maps to vanilla DensityFunctions.Clamp
//Restricts the input output to the range [min, max]
public sealed class Clamp : Mapped
{
    public double Min { get; }
    public double Max { get; }

    public Clamp(DensityFunction input, double min, double max) : base(input)
    {
        Min = min;
        Max = max;
    }

    public override double Compute(FunctionContext context)
    {
        var v = Input.Compute(context);
        if (v < Min) return Min;
        if (v > Max) return Max;
        return v;
    }

    public override DensityFunction MapChildren(Visitor visitor)
        => new Clamp(visitor.Apply(Input), Min, Max);

    public override double MinValue => Min;
    public override double MaxValue => Max;
}

//MulOrAdd mul-or-add density function, maps to vanilla DensityFunctions.MulOrAdd
//type=ADD gives output = input + value; type=MUL gives output = input * value
public sealed class MulOrAdd : Mapped
{
    public enum OpType { Add, Mul }

    public OpType Type { get; }
    public double Value { get; }

    private MulOrAdd(OpType type, double value, DensityFunction input) : base(input)
    {
        Type = type;
        Value = value;
    }

    //Of factory method, maps to vanilla MulOrAdd.create
    public static MulOrAdd Of(OpType type, double value, DensityFunction input)
        => new(type, value, input);

    public override double Compute(FunctionContext context)
    {
        var v = Input.Compute(context);
        return Type == OpType.Add ? v + Value : v * Value;
    }

    public override DensityFunction MapChildren(Visitor visitor)
        => new MulOrAdd(Type, Value, visitor.Apply(Input));

    public override double MinValue => Type == OpType.Add ? Input.MinValue + Value : Input.MinValue * Value;
    public override double MaxValue => Type == OpType.Add ? Input.MaxValue + Value : Input.MaxValue * Value;
}

//Ap2 binary operation density function, maps to vanilla DensityFunctions.Ap2
//Supports the four binary operations Max/Min/Add/Mul
public sealed class Ap2 : DensityFunction
{
    public enum OpType { Max, Min, Add, Mul }

    public OpType Type { get; }
    public DensityFunction Input1 { get; }
    public DensityFunction Input2 { get; }

    public Ap2(OpType type, DensityFunction input1, DensityFunction input2)
    {
        Type = type;
        Input1 = input1;
        Input2 = input2;
    }

    public double Compute(FunctionContext context)
    {
        var a = Input1.Compute(context);
        var b = Input2.Compute(context);
        return Type switch
        {
            OpType.Max => Math.Max(a, b),
            OpType.Min => Math.Min(a, b),
            OpType.Add => a + b,
            OpType.Mul => a * b,
            _ => throw new NotSupportedException($"Unsupported Ap2 type: {Type}")
        };
    }

    public void FillArray(double[] output, ContextProvider contextProvider)
    {
        for (var i = 0; i < output.Length; i++)
            output[i] = Compute(contextProvider.ForIndex(i));
    }

    public DensityFunction MapChildren(Visitor visitor)
        => new Ap2(Type, visitor.Apply(Input1), visitor.Apply(Input2));

    public double MinValue => Type switch
    {
        OpType.Max => Math.Max(Input1.MinValue, Input2.MinValue),
        OpType.Min => Math.Min(Input1.MinValue, Input2.MinValue),
        OpType.Add => Input1.MinValue + Input2.MinValue,
        OpType.Mul => Input1.MinValue * Input2.MinValue,
        _ => 0.0
    };

    public double MaxValue => Type switch
    {
        OpType.Max => Math.Max(Input1.MaxValue, Input2.MaxValue),
        OpType.Min => Math.Min(Input1.MaxValue, Input2.MaxValue),
        OpType.Add => Input1.MaxValue + Input2.MaxValue,
        OpType.Mul => Input1.MaxValue * Input2.MaxValue,
        _ => 0.0
    };
}

//YClampedGradient Y-axis clamped gradient density function, maps to vanilla DensityFunctions.YClampedGradient
//Linear interpolation from fromValue..toValue over fromY..toY; out of range takes the end value
public sealed class YClampedGradient : DensityFunctions.Marker
{
    public int FromY { get; }
    public int ToY { get; }
    public double FromValue { get; }
    public double ToValue { get; }

    public YClampedGradient(int fromY, int toY, double fromValue, double toValue)
    {
        FromY = fromY;
        ToY = toY;
        FromValue = fromValue;
        ToValue = toValue;
    }

    public double Compute(FunctionContext context)
    {
        var y = context.BlockY;
        if (y <= FromY) return FromValue;
        if (y >= ToY) return ToValue;
        var t = (double)(y - FromY) / (ToY - FromY);
        return FromValue + (ToValue - FromValue) * t;
    }

    public void FillArray(double[] output, ContextProvider contextProvider)
        => contextProvider.FillAllDirectly(output, this);

    public double MinValue => Math.Min(FromValue, ToValue);
    public double MaxValue => Math.Max(FromValue, ToValue);
}

//ShiftNoise noise shift density function interface, maps to vanilla DensityFunctions.ShiftNoise
//Holds offsetNoise; scales the coordinate by 0.25, samples and multiplies by 4, feeding ShiftedNoise's shiftX/Y/Z offsets
//min/max are ±4 times offsetNoise.MaxValue
public interface ShiftNoise : DensityFunction
{
    NoiseHolder OffsetNoise { get; }

    //SampleLocal local-coordinate sampling, maps to vanilla ShiftNoise.compute(localX, localY, localZ)
    //Static helper the Compute overrides delegate to; scales the coordinate by 0.25, samples the noise and multiplies by 4
    static double SampleLocal(NoiseHolder noise, double localX, double localY, double localZ)
        => noise.GetValue(localX * 0.25, localY * 0.25, localZ * 0.25) * 4.0;

    double DensityFunction.MinValue => -MaxValue;
    double DensityFunction.MaxValue => OffsetNoise.MaxValue * 4.0;

    void DensityFunction.FillArray(double[] output, ContextProvider contextProvider)
        => contextProvider.FillAllDirectly(output, this);
}

//ShiftA XZ-plane shift noise, maps to vanilla DensityFunctions.ShiftA
//compute takes the local blockX and blockZ with Y zeroed, feeding ShiftedNoise's X offset
public sealed class ShiftA : ShiftNoise
{
    public ShiftA(NoiseHolder offsetNoise) { OffsetNoise = offsetNoise; }

    public NoiseHolder OffsetNoise { get; }

    public double Compute(FunctionContext context)
        => ShiftNoise.SampleLocal(OffsetNoise, context.BlockX, 0.0, context.BlockZ);

    public DensityFunction MapChildren(Visitor visitor) => new ShiftA(visitor.VisitNoise(OffsetNoise));
}

//ShiftB ZX crossed shift noise, maps to vanilla DensityFunctions.ShiftB
//compute takes the local blockZ and blockX with Y zeroed, feeding ShiftedNoise's Z offset
public sealed class ShiftB : ShiftNoise
{
    public ShiftB(NoiseHolder offsetNoise) { OffsetNoise = offsetNoise; }

    public NoiseHolder OffsetNoise { get; }

    public double Compute(FunctionContext context)
        => ShiftNoise.SampleLocal(OffsetNoise, context.BlockZ, context.BlockX, 0.0);

    public DensityFunction MapChildren(Visitor visitor) => new ShiftB(visitor.VisitNoise(OffsetNoise));
}

//Shift three-axis shift noise, maps to vanilla DensityFunctions.Shift
//compute takes all local blockX/Y/Z, feeding ShiftedNoise's Y offset
public sealed class Shift : ShiftNoise
{
    public Shift(NoiseHolder offsetNoise) { OffsetNoise = offsetNoise; }

    public NoiseHolder OffsetNoise { get; }

    public double Compute(FunctionContext context)
        => ShiftNoise.SampleLocal(OffsetNoise, context.BlockX, context.BlockY, context.BlockZ);

    public DensityFunction MapChildren(Visitor visitor) => new Shift(visitor.VisitNoise(OffsetNoise));
}

//ShiftedNoise shifted noise density function, maps to vanilla DensityFunctions.ShiftedNoise
//Computes the coordinate offset from the shiftX/Y/Z functions before calling the inner Noise
public sealed class ShiftedNoise : DensityFunction
{
    public NoiseHolder NoiseData { get; }
    public double XzScale { get; }
    public double YScale { get; }
    public DensityFunction ShiftX { get; }
    public DensityFunction ShiftY { get; }
    public DensityFunction ShiftZ { get; }

    public ShiftedNoise(NoiseHolder noise, double xzScale, double yScale,
        DensityFunction shiftX, DensityFunction shiftY, DensityFunction shiftZ)
    {
        NoiseData = noise;
        XzScale = xzScale;
        YScale = yScale;
        ShiftX = shiftX;
        ShiftY = shiftY;
        ShiftZ = shiftZ;
    }

    public double Compute(FunctionContext context)
    {
        var x = context.BlockX * XzScale + ShiftX.Compute(context);
        var y = context.BlockY * YScale + ShiftY.Compute(context);
        var z = context.BlockZ * XzScale + ShiftZ.Compute(context);
        return NoiseData.GetValue(x, y, z);
    }

    public void FillArray(double[] output, ContextProvider contextProvider)
    {
        for (var i = 0; i < output.Length; i++)
            output[i] = Compute(contextProvider.ForIndex(i));
    }

    //MapChildren replaces the noise reference and the three shift functions, maps to vanilla mapChildren
    public DensityFunction MapChildren(Visitor visitor)
        => new ShiftedNoise(visitor.VisitNoise(NoiseData), XzScale, YScale,
            visitor.Apply(ShiftX), visitor.Apply(ShiftY), visitor.Apply(ShiftZ));

    public double MinValue => -NoiseData.MaxValue;
    public double MaxValue => NoiseData.MaxValue;
}
