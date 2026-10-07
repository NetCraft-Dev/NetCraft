using NetCraft.Codec;
using NetCraft.Game.World.Level.LevelGen.Synth;

namespace NetCraft.Game.World.Level.LevelGen;

//DensityFunction density function interface, maps to vanilla net.minecraft.world.level.levelgen.DensityFunction
//The core world generation abstraction; samples a density value by coordinate for terrain/cave/ore distribution decisions
//Sub-interfaces SimpleFunction/Marker distinguish simple constants from transforms
//FunctionContext provides coordinate access; Visitor replaces child nodes
//The Codec covariance issue is solved by each subclass exposing a static readonly CodecInstance field, not declared here
public interface DensityFunction
{
    //Compute samples the density value from the context coordinate
    double Compute(FunctionContext context);

    //FillArray batch-samples into an array, maps to vanilla fillArray
    void FillArray(double[] output, ContextProvider contextProvider);

    //MapChildren replaces child nodes, maps to vanilla mapChildren
    DensityFunction MapChildren(Visitor visitor);

    //MapAll replaces the whole node, maps to vanilla mapAll
    //Vanilla uses a recursive wrapper visitor so apply calls inside nodes also expand children; plain Apply(this) would leave subtrees unvisited
    DensityFunction MapAll(Visitor visitor) => new RecursiveVisitor(visitor).Apply(this);

    double MinValue { get; }
    double MaxValue { get; }

    //Clamp clamp wrapper, maps to vanilla DensityFunction.clamp(min, max)
    //The default builds new Clamp(this, min, max); subclasses may override with an optimised path
    DensityFunction Clamp(double min, double max) => new Clamp(this, min, max);

    //Abs absolute value wrapper, maps to vanilla DensityFunction.abs
    DensityFunction Abs() => new MappedTypes.Abs(this);

    //Square square wrapper, maps to vanilla DensityFunction.square
    DensityFunction Square() => new MappedTypes.Square(this);

    //Cube cube wrapper, maps to vanilla DensityFunction.cube
    DensityFunction Cube() => new MappedTypes.Cube(this);

    //HalfNegative halve-negative wrapper, maps to vanilla DensityFunction.halfNegative
    DensityFunction HalfNegative() => new MappedTypes.HalfNegative(this);

    //QuarterNegative quarter-negative wrapper, maps to vanilla DensityFunction.quarterNegative
    DensityFunction QuarterNegative() => new MappedTypes.QuarterNegative(this);

    //Squeeze squeeze wrapper, maps to vanilla DensityFunction.squeeze
    DensityFunction Squeeze() => new MappedTypes.Squeeze(this);

    //Invert reciprocal wrapper, maps to vanilla DensityFunction.invert
    DensityFunction Invert() => new MappedTypes.Invert(this);
}

//FunctionContext function context providing the sample coordinate, maps to vanilla DensityFunction.FunctionContext
public interface FunctionContext
{
    int BlockX { get; }
    int BlockY { get; }
    int BlockZ { get; }
}

//ContextProvider produces a FunctionContext by index, maps to vanilla DensityFunction.ContextProvider
public interface ContextProvider
{
    FunctionContext ForIndex(int index);
    void FillAllDirectly(double[] output, DensityFunction function);
}

//SimpleFunction simple function interface, maps to vanilla DensityFunction.SimpleFunction
//Constants and transforms that do not depend on the context coordinate get default fillArray/mapChildren behaviour
public interface SimpleFunction : DensityFunction
{
    //FillArray simple functions fill in bulk directly to avoid per-coordinate sampling
    void DensityFunction.FillArray(double[] output, ContextProvider contextProvider)
        => contextProvider.FillAllDirectly(output, this);

    //MapChildren simple functions have no children so they return themselves
    DensityFunction DensityFunction.MapChildren(Visitor visitor) => this;
}

//Visitor visitor interface, maps to vanilla DensityFunction.Visitor
//apply replaces nodes, visitNoise replaces noise references
public interface Visitor
{
    DensityFunction Apply(DensityFunction input);

    //VisitNoise replaces a noise reference; the default returns the original
    NoiseHolder VisitNoise(NoiseHolder noise) => noise;
}

//RecursiveVisitor recursive wrapper visitor, maps to the anonymous RecursiveVisitor inside vanilla mapAll
//Turns Apply into expanding children first then handing off to the real visitor, so the whole density tree is visited
internal sealed class RecursiveVisitor : Visitor
{
    private readonly Visitor _inner;

    public RecursiveVisitor(Visitor inner) => _inner = inner;

    public DensityFunction Apply(DensityFunction input) => _inner.Apply(input.MapChildren(this));

    public NoiseHolder VisitNoise(NoiseHolder noise) => _inner.VisitNoise(noise);
}

//SinglePointContext single-point context, maps to vanilla DensityFunction.SinglePointContext
//Mutable: a per-cell caller reuses one instance; allocating one per cell in a nearly hundred-thousand-cell loop like the aquifer is pure waste
//Requires the reuser to own the instance exclusively and the called density functions not to retain the context; the reuser guarantees both
public sealed class SinglePointContext : FunctionContext
{
    public int BlockX { get; private set; }
    public int BlockY { get; private set; }
    public int BlockZ { get; private set; }

    public SinglePointContext(int blockX, int blockY, int blockZ) => Set(blockX, blockY, blockZ);

    //At static factory building from coordinates; callers needing an independent instance use it
    public static SinglePointContext At(int x, int y, int z) => new(x, y, z);

    //Set rewrites the coordinates for reuse and returns itself for chaining
    public SinglePointContext Set(int x, int y, int z)
    {
        BlockX = x;
        BlockY = y;
        BlockZ = z;
        return this;
    }
}

//NoiseHolder noise holder, maps to vanilla DensityFunction.NoiseHolder
//Wraps NoiseParameters data and an optional NormalNoise instance; noise is null during data deserialisation
public sealed class NoiseHolder
{
    public NoiseParameters? NoiseData { get; }
    public NormalNoise? Noise { get; }

    public NoiseHolder(NoiseParameters? noiseData, NormalNoise? noise)
    {
        NoiseData = noiseData;
        Noise = noise;
    }

    public NoiseHolder(NoiseParameters? noiseData) : this(noiseData, null) { }

    //GetValue samples by coordinate and returns 0 when Noise is null, the fallback for empty data in vanilla
    public double GetValue(double x, double y, double z)
        => Noise?.GetValue(x, y, z) ?? 0.0;

    //MaxValue wrapped noise maximum, falling back to 2.0 when Noise is null
    public double MaxValue => Noise?.MaxValue ?? 2.0;
}
