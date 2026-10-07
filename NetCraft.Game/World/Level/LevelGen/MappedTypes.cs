using NetCraft.Game.World.Level.LevelGen.Synth;

namespace NetCraft.Game.World.Level.LevelGen;

//MappedTypes the unary-transform density function subclasses, the concrete subclasses of vanilla DensityFunctions.Mapped
//Seven transforms: Abs/Square/Cube/HalfNegative/QuarterNegative/Invert/Squeeze
//Work with the DensityFunctions.Mapped abstract base to build the NoiseRouterData density tree
public static class MappedTypes
{
    //MappedType enum, maps to vanilla DensityFunctions.Mapped.Type
    public enum MappedType
    {
        Abs,
        Square,
        Cube,
        HalfNegative,
        QuarterNegative,
        Invert,
        Squeeze
    }

    //Abs absolute value, maps to vanilla Mapped.Type.ABS
    public sealed class Abs : Mapped
    {
        public Abs(DensityFunction input) : base(input) { }
        public override double Compute(FunctionContext context) => Math.Abs(Input.Compute(context));
        public override DensityFunction MapChildren(Visitor visitor) => new Abs(visitor.Apply(Input));
        public override double MinValue => Math.Max(0.0, Input.MinValue);
        public override double MaxValue => Math.Max(Math.Abs(Input.MinValue), Math.Abs(Input.MaxValue));
    }

    //Square square, maps to vanilla Mapped.Type.SQUARE
    public sealed class Square : Mapped
    {
        public Square(DensityFunction input) : base(input) { }
        public override double Compute(FunctionContext context)
        {
            var v = Input.Compute(context);
            return v * v;
        }
        public override DensityFunction MapChildren(Visitor visitor) => new Square(visitor.Apply(Input));
        public override double MinValue => Math.Max(0.0, Input.MinValue);
        public override double MaxValue => Math.Max(Math.Abs(Input.MinValue), Math.Abs(Input.MaxValue));
    }

    //Cube cube, maps to vanilla Mapped.Type.CUBE
    public sealed class Cube : Mapped
    {
        public Cube(DensityFunction input) : base(input) { }
        public override double Compute(FunctionContext context)
        {
            var v = Input.Compute(context);
            return v * v * v;
        }
        public override DensityFunction MapChildren(Visitor visitor) => new Cube(visitor.Apply(Input));
        public override double MinValue
        {
            get
            {
                var a = Input.MinValue;
                var b = Input.MaxValue;
                if (a >= 0.0) return a * a * a;
                if (b <= 0.0) return b * b * b;
                return 0.0;
            }
        }
        public override double MaxValue
        {
            get
            {
                var a = Input.MinValue;
                var b = Input.MaxValue;
                if (a >= 0.0) return b * b * b;
                if (b <= 0.0) return a * a * a;
                return Math.Max(a * a * a, b * b * b);
            }
        }
    }

    //HalfNegative halves negative values, maps to vanilla Mapped.Type.HALF_NEGATIVE
    //Positive values pass through, negatives are multiplied by 0.5
    public sealed class HalfNegative : Mapped
    {
        public HalfNegative(DensityFunction input) : base(input) { }
        public override double Compute(FunctionContext context)
        {
            var v = Input.Compute(context);
            return v > 0.0 ? v : v * 0.5;
        }
        public override DensityFunction MapChildren(Visitor visitor) => new HalfNegative(visitor.Apply(Input));
        public override double MinValue => Input.MinValue * 0.5;
        public override double MaxValue => Input.MaxValue;
    }

    //QuarterNegative quarters negative values, maps to vanilla Mapped.Type.QUARTER_NEGATIVE
    //Positive values pass through, negatives are multiplied by 0.25
    public sealed class QuarterNegative : Mapped
    {
        public QuarterNegative(DensityFunction input) : base(input) { }
        public override double Compute(FunctionContext context)
        {
            var v = Input.Compute(context);
            return v > 0.0 ? v : v * 0.25;
        }
        public override DensityFunction MapChildren(Visitor visitor) => new QuarterNegative(visitor.Apply(Input));
        public override double MinValue => Input.MinValue * 0.25;
        public override double MaxValue => Input.MaxValue;
    }

    //Invert reciprocal, maps to vanilla Mapped.Type.INVERT
    public sealed class Invert : Mapped
    {
        public Invert(DensityFunction input) : base(input) { }
        public override double Compute(FunctionContext context) => 1.0 / Input.Compute(context);
        public override DensityFunction MapChildren(Visitor visitor) => new Invert(visitor.Apply(Input));
        public override double MinValue => double.NegativeInfinity;
        public override double MaxValue => double.PositiveInfinity;
    }

    //Squeeze squeeze transform, maps to vanilla Mapped.Type.SQUEEZE
    //Clamp to [-1,1] then c/2 - c^3/24
    public sealed class Squeeze : Mapped
    {
        public Squeeze(DensityFunction input) : base(input) { }
        public override double Compute(FunctionContext context)
        {
            var v = Input.Compute(context);
            var c = Math.Clamp(v, -1.0, 1.0);
            return (c / 2.0) - (c * c * c / 24.0);
        }
        public override DensityFunction MapChildren(Visitor visitor) => new Squeeze(visitor.Apply(Input));
        public override double MinValue => -0.4583333333333333;
        public override double MaxValue => 0.4583333333333333;
    }

    //Create factory, maps to vanilla Mapped.create
    public static Mapped Create(MappedType type, DensityFunction input) => type switch
    {
        MappedType.Abs => new Abs(input),
        MappedType.Square => new Square(input),
        MappedType.Cube => new Cube(input),
        MappedType.HalfNegative => new HalfNegative(input),
        MappedType.QuarterNegative => new QuarterNegative(input),
        MappedType.Invert => new Invert(input),
        MappedType.Squeeze => new Squeeze(input),
        _ => throw new NotSupportedException($"Unsupported MappedType: {type}")
    };
}

//HolderHolder holds a DensityFunction reference directly, a simplified version of vanilla DensityFunctions.HolderHolder
//Vanilla wraps it in Holder<DensityFunction> to support Codec references by ResourceKey; NetCraft does not persist through Codec yet and references directly
//RandomState's noiseFlattener expands HolderHolder into the inner function to cut down call depth
public sealed class HolderHolder : DensityFunction
{
    public DensityFunction Function { get; }

    public HolderHolder(DensityFunction function) { Function = function; }

    public double Compute(FunctionContext context) => Function.Compute(context);

    public void FillArray(double[] output, ContextProvider contextProvider)
        => Function.FillArray(output, contextProvider);

    public DensityFunction MapChildren(Visitor visitor) => new HolderHolder(visitor.Apply(Function));

    public double MinValue => Function.MinValue;
    public double MaxValue => Function.MaxValue;
}
