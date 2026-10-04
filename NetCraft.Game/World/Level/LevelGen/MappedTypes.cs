using NetCraft.Game.World.Level.LevelGen.Synth;

namespace NetCraft.Game.World.Level.LevelGen;

//MappedTypes 一元变换密度函数子类对应原版 DensityFunctions.Mapped 的具体子类
//Abs/Square/Cube/HalfNegative/QuarterNegative/Invert/Squeeze 七种变换
//配合 DensityFunctions.Mapped 抽象基类支持 NoiseRouterData 密度树构建
public static class MappedTypes
{
    //MappedType 枚举对应原版 DensityFunctions.Mapped.Type
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

    //Abs 绝对值变换对应原版 Mapped.Type.ABS
    public sealed class Abs : Mapped
    {
        public Abs(DensityFunction input) : base(input) { }
        public override double Compute(FunctionContext context) => Math.Abs(Input.Compute(context));
        public override DensityFunction MapChildren(Visitor visitor) => new Abs(visitor.Apply(Input));
        public override double MinValue => Math.Max(0.0, Input.MinValue);
        public override double MaxValue => Math.Max(Math.Abs(Input.MinValue), Math.Abs(Input.MaxValue));
    }

    //Square 平方变换对应原版 Mapped.Type.SQUARE
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

    //Cube 三次方变换对应原版 Mapped.Type.CUBE
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

    //HalfNegative 负值减半对应原版 Mapped.Type.HALF_NEGATIVE
    //正值原样返回负值乘 0.5
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

    //QuarterNegative 负值减四分之一对应原版 Mapped.Type.QUARTER_NEGATIVE
    //正值原样返回负值乘 0.25
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

    //Invert 倒数对应原版 Mapped.Type.INVERT
    public sealed class Invert : Mapped
    {
        public Invert(DensityFunction input) : base(input) { }
        public override double Compute(FunctionContext context) => 1.0 / Input.Compute(context);
        public override DensityFunction MapChildren(Visitor visitor) => new Invert(visitor.Apply(Input));
        public override double MinValue => double.NegativeInfinity;
        public override double MaxValue => double.PositiveInfinity;
    }

    //Squeeze 挤压变换对应原版 Mapped.Type.SQUEEZE
    //先 clamp 到 [-1,1]再 c/2 - c^3/24
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

    //Create 工厂对应原版 Mapped.create
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

//HolderHolder 直接持有 DensityFunction 引用对应原版 DensityFunctions.HolderHolder 简化版
//原版用 Holder<DensityFunction> 包装支持 Codec 引用 ResourceKeyNetCraft 暂不做 Codec 持久化直接引用
//RandomState 的 noiseFlattener 会展开 HolderHolder 为内部函数以减少调用层级
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
