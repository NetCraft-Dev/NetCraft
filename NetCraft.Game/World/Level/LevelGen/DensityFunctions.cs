using NetCraft.Codec;
using NetCraft.Game.Util;
using NetCraft.Game.World.Level.LevelGen.Synth;

namespace NetCraft.Game.World.Level.LevelGen;

//DensityFunctions 密度函数集合对应原版 net.minecraft.world.level.levelgen.DensityFunctions
//集中放置常量/噪声/变换/钳制/乘加/二元运算等具体 DensityFunction 实现
//Codec 注册到 DENSITY_FUNCTION_TYPE 注册表待 dispatch codec 子系统就绪后接入
public static class DensityFunctions
{
    //Constant.ConstantValue Codec 直接 double 序列化对齐原版 Constant.CODEC
    public static readonly Codec<Constant> ConstantCodec =
        Codecs.Double.ComapFlatMap(
            v => DataResult<Constant>.Success(new Constant(v)),
            c => c.Value);

    //YClampedGradientCodec 简化为常量字段对齐原版 YClampedGradient.CODEC
    public static readonly Codec<YClampedGradient> YClampedGradientCodec =
        RecordCodecBuilder.Of4(
            Codecs.Int.FieldOf("from_y").ForGetter<YClampedGradient, int>(g => g.FromY),
            Codecs.Int.FieldOf("to_y").ForGetter<YClampedGradient, int>(g => g.ToY),
            Codecs.Double.FieldOf("from_value").ForGetter<YClampedGradient, double>(g => g.FromValue),
            Codecs.Double.FieldOf("to_value").ForGetter<YClampedGradient, double>(g => g.ToValue),
            (fromY, toY, fromValue, toValue) => new YClampedGradient(fromY, toY, fromValue, toValue));

    //Marker 标记接口对应原版 DensityFunctions.Marker
    //标识无子节点的叶子密度函数只覆盖 mapChildren 返回自身 不覆盖 mapAll 否则子树不会被访问
    public interface Marker : DensityFunction
    {
        //MapChildren 无子节点返回自身
        DensityFunction DensityFunction.MapChildren(Visitor visitor) => this;
    }

    //工厂方法集合对应原版 DensityFunctions 静态工厂
    //NoiseRouterData 用此构造 overworld/nether/end 等密度树

    //Zero 零常量对应原版 zero
    public static DensityFunction Zero() => NetCraft.Game.World.Level.LevelGen.Constant.Zero;

    //ConstantValue 常量对应原版 constant
    //方法名避开与 Constant 类名冲突C# 同名时方法组优先于类型会编译失败
    public static DensityFunction ConstantValue(double value) => new Constant(value);

    //YClampedGradient Y 轴钳制梯度对应原版 yClampedGradient
    public static DensityFunction YClampedGradient(int fromY, int toY, double fromValue, double toValue)
        => new YClampedGradient(fromY, toY, fromValue, toValue);

    //Add 二元加对应原版 add带 Constant 折叠优化为 MulOrAdd
    public static DensityFunction Add(DensityFunction a, DensityFunction b)
        => TwoArgumentCreate(Ap2.OpType.Add, a, b);

    //Mul 二元乘对应原版 mul带 Constant 折叠优化为 MulOrAdd
    public static DensityFunction Mul(DensityFunction a, DensityFunction b)
        => TwoArgumentCreate(Ap2.OpType.Mul, a, b);

    //Min 二元最小对应原版 min
    public static DensityFunction Min(DensityFunction a, DensityFunction b)
        => new Ap2(Ap2.OpType.Min, a, b);

    //Max 二元最大对应原版 max
    public static DensityFunction Max(DensityFunction a, DensityFunction b)
        => new Ap2(Ap2.OpType.Max, a, b);

    //TwoArgumentCreate 二元运算工厂对应原版 TwoArgumentSimpleFunction.create
    //Add/Mul 时若任一参数为 Constant 折叠为 MulOrAdd 避免无谓嵌套
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

    //Lerp 三参线性插值对应原版 lerp(alpha, first, second)
    //first 为 Constant 时走简化路径否则用 cacheOnce 缓存 alpha 避免重复采样
    public static DensityFunction Lerp(DensityFunction alpha, DensityFunction first, DensityFunction second)
    {
        if (first is Constant c)
            return Lerp(alpha, c.Value, second);
        var cached = CacheOnce(alpha);
        var oneMinus = Add(Mul(cached, ConstantValue(-1.0)), ConstantValue(1.0));
        return Add(Mul(first, oneMinus), Mul(second, cached));
    }

    //Lerp 常量版对应原版 lerp(factor, firstValue, second)
    //返回 mul(factor, second - firstValue) + firstValue
    public static DensityFunction Lerp(DensityFunction factor, double first, DensityFunction second)
        => Add(Mul(factor, Add(second, ConstantValue(-first))), ConstantValue(first));

    //Clamp 钳制对应原版 clamp
    public static DensityFunction Clamp(DensityFunction input, double min, double max)
        => new Clamp(input, min, max);

    //Interpolated 插值标记对应原版 interpolated
    public static DensityFunction Interpolated(DensityFunction function)
        => DensityFunctionsExtra.Interpolated(function);

    //FlatCache 平面缓存对应原版 flatCache
    public static DensityFunction FlatCache(DensityFunction function)
        => DensityFunctionsExtra.FlatCache(function);

    //Cache2D 二维缓存对应原版 cache2d
    public static DensityFunction Cache2D(DensityFunction function)
        => DensityFunctionsExtra.Cache2D(function);

    //CacheOnce 单次缓存对应原版 cacheOnce
    public static DensityFunction CacheOnce(DensityFunction function)
        => DensityFunctionsExtra.CacheOnce(function);

    //BlendDensity 混合密度标记对应原版 blendDensity
    public static DensityFunction BlendDensity(DensityFunction function)
        => DensityFunctionsExtra.BlendDensity(function);

    //Spline 样条密度函数对应原版 spline
    public static DensityFunction Spline(CubicSpline spline)
        => DensityFunctionsExtra.Spline(spline);

    //Noise 噪声密度函数对应原版 noise(holder)
    public static DensityFunction Noise(NoiseParameters noiseData)
        => Noise(noiseData, 1.0, 1.0);

    //Noise 带缩放噪声对应原版 noise(holder, xzScale, yScale)
    public static DensityFunction Noise(NoiseParameters noiseData, double xzScale, double yScale)
        => new Noise(new NoiseHolder(noiseData), xzScale, yScale);

    //Noise Y 缩放版对应原版 noise(holder, yScale) 简化为 xzScale=1
    public static DensityFunction Noise(NoiseParameters noiseData, double yScale)
        => Noise(noiseData, 1.0, yScale);

    //MappedNoise 单位区间映射噪声对应原版 mappedNoise(holder, xzScale, yScale, minTarget, maxTarget)
    //把噪声 [-1,1] 区间映射到 [minTarget, maxTarget]
    public static DensityFunction MappedNoise(NoiseParameters noiseData, double xzScale, double yScale, double minTarget, double maxTarget)
        => MapFromUnitTo(Noise(noiseData, xzScale, yScale), minTarget, maxTarget);

    //MappedNoise 简化版对应原版 mappedNoise(holder, yScale, minTarget, maxTarget)
    public static DensityFunction MappedNoise(NoiseParameters noiseData, double yScale, double minTarget, double maxTarget)
        => MappedNoise(noiseData, 1.0, yScale, minTarget, maxTarget);

    //MappedNoise 双 1 缩放对应原版 mappedNoise(holder, minTarget, maxTarget)
    public static DensityFunction MappedNoise(NoiseParameters noiseData, double minTarget, double maxTarget)
        => MappedNoise(noiseData, 1.0, 1.0, minTarget, maxTarget);

    //MapFromUnitTo 单位区间映射对应原版 mapFromUnitTo
    //middle = (min+max)/2, factor = (max-min)/2, 输出 = middle + factor * input
    private static DensityFunction MapFromUnitTo(DensityFunction function, double min, double max)
    {
        var middle = (min + max) * 0.5;
        var factor = (max - min) * 0.5;
        return Add(ConstantValue(middle), Mul(ConstantValue(factor), function));
    }

    //RangeChoice 范围选择对应原版 rangeChoice
    public static DensityFunction RangeChoice(DensityFunction input, double minInclusive, double maxExclusive,
        DensityFunction whenInRange, DensityFunction whenOutOfRange)
        => DensityFunctionsExtra.RangeChoice(input, minInclusive, maxExclusive, whenInRange, whenOutOfRange);

    //IntervalSelect 多段选择对应原版 intervalSelect
    public static DensityFunction IntervalSelect(DensityFunction input, double[] thresholds, DensityFunction[] functions)
        => DensityFunctionsExtra.IntervalSelect(input, thresholds, functions);

    //ShiftA/ShiftB/Shift 噪声偏移对应原版 shiftA/shiftB/shift
    public static DensityFunction ShiftA(NoiseParameters noiseData) => new ShiftA(new NoiseHolder(noiseData));
    public static DensityFunction ShiftB(NoiseParameters noiseData) => new ShiftB(new NoiseHolder(noiseData));
    public static DensityFunction Shift(NoiseParameters noiseData) => new Shift(new NoiseHolder(noiseData));

    //ShiftedNoise2d 二维偏移噪声对应原版 shiftedNoise2d
    //shiftY 用 Constant.Zero 占位原版 zero()
    public static DensityFunction ShiftedNoise2d(DensityFunction shiftX, DensityFunction shiftZ, double xzScale, NoiseParameters noiseData)
        => new ShiftedNoise(new NoiseHolder(noiseData), xzScale, 0.0, shiftX, Zero(), shiftZ);

    //EndIslands 末地岛屿密度对应原版 endIslands
    public static DensityFunction EndIslands(long seed) => DensityFunctionsExtra.EndIslands(seed);

    //FindTopSurface 查找顶部表面对应原版 findTopSurface
    public static DensityFunction FindTopSurface(DensityFunction density, DensityFunction upperBound, int lowerBound, int stepSize)
        => new FindTopSurface(density, upperBound, lowerBound, stepSize);

    //BlendAlpha 混合透明度单例对应原版 blendAlpha
    public static DensityFunction BlendAlpha() => NetCraft.Game.World.Level.LevelGen.BlendAlpha.Instance;

    //BlendOffset 混合偏移单例对应原版 blendOffset
    public static DensityFunction BlendOffset() => NetCraft.Game.World.Level.LevelGen.BlendOffset.Instance;

    //Abs 一元绝对值对应原版 map(input, ABS)
    public static DensityFunction Abs(DensityFunction input) => new MappedTypes.Abs(input);

    //Square 一元平方对应原版 map(input, SQUARE)
    public static DensityFunction Square(DensityFunction input) => new MappedTypes.Square(input);

    //Cube 一元三次方对应原版 map(input, CUBE)
    public static DensityFunction Cube(DensityFunction input) => new MappedTypes.Cube(input);

    //HalfNegative 一元负值减半对应原版 map(input, HALF_NEGATIVE)
    public static DensityFunction HalfNegative(DensityFunction input) => new MappedTypes.HalfNegative(input);

    //QuarterNegative 一元负值减四分之一对应原版 map(input, QUARTER_NEGATIVE)
    public static DensityFunction QuarterNegative(DensityFunction input) => new MappedTypes.QuarterNegative(input);

    //Invert 一元倒数对应原版 map(input, INVERT)
    public static DensityFunction Invert(DensityFunction input) => new MappedTypes.Invert(input);

    //Squeeze 一元挤压对应原版 map(input, SQUEEZE)
    public static DensityFunction Squeeze(DensityFunction input) => new MappedTypes.Squeeze(input);
}

//Constant 常量密度函数对应原版 DensityFunctions.Constant
//所有坐标返回固定值用于偏移/缩放基线
public sealed class Constant : DensityFunctions.Marker
{
    public double Value { get; }

    public Constant(double value) { Value = value; }

    public double Compute(FunctionContext context) => Value;

    public void FillArray(double[] output, ContextProvider contextProvider)
        => contextProvider.FillAllDirectly(output, this);

    public double MinValue => Value;
    public double MaxValue => Value;

    //Zero 与 One 预定义常量对应原版 ConstantZero/ConstantOne
    public static readonly Constant Zero = new(0.0);
    public static readonly Constant One = new(1.0);
}

//Noise 噪声密度函数对应原版 DensityFunctions.Noise
//包装 NoiseHolder 按坐标采样输出噪声值 不是叶子节点必须让 visitor 访问到噪声引用
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

    //MapChildren 替换噪声引用对应原版 mapChildren 里的 visitNoise
    public DensityFunction MapChildren(Visitor visitor)
        => new Noise(visitor.VisitNoise(NoiseData), XzScale, YScale);

    public double MinValue => -NoiseData.MaxValue;
    public double MaxValue => NoiseData.MaxValue;
}

//Mapped 映射密度函数基类对应原版 DensityFunctions.Mapped
//持有 input 子函数子类按需变换 compute/min/max
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

//Clamp 钳制密度函数对应原版 DensityFunctions.Clamp
//将 input 输出限制在 [min, max] 范围
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

//MulOrAdd 乘加密度函数对应原版 DensityFunctions.MulOrAdd
//type=ADD 时 output = input + valuetype=MUL 时 output = input * value
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

    //Of 工厂方法对应原版 MulOrAdd.create
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

//Ap2 二元运算密度函数对应原版 DensityFunctions.Ap2
//支持 Max/Min/Add/Mul 四种二元运算
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

//YClampedGradient Y 轴钳制梯度密度函数对应原版 DensityFunctions.YClampedGradient
//在 fromY..toY 区间内线性插值 fromValue..toValue 超出范围取端点值
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

//ShiftNoise 噪声偏移密度函数接口对应原版 DensityFunctions.ShiftNoise
//持有 offsetNoise 把坐标缩放 0.25 后采样放大 4 倍输出用于 ShiftedNoise 的 shiftX/Y/Z 偏移量
//min/max 取 offsetNoise.MaxValue 的 4 倍正负对称
public interface ShiftNoise : DensityFunction
{
    NoiseHolder OffsetNoise { get; }

    //SampleLocal 局部坐标采样对应原版 ShiftNoise.compute(localX, localY, localZ)
    //静态辅助子类 Compute 委托把坐标缩放 0.25 后取噪声值放大 4 倍
    static double SampleLocal(NoiseHolder noise, double localX, double localY, double localZ)
        => noise.GetValue(localX * 0.25, localY * 0.25, localZ * 0.25) * 4.0;

    double DensityFunction.MinValue => -MaxValue;
    double DensityFunction.MaxValue => OffsetNoise.MaxValue * 4.0;

    void DensityFunction.FillArray(double[] output, ContextProvider contextProvider)
        => contextProvider.FillAllDirectly(output, this);
}

//ShiftA XZ 平面偏移噪声对应原版 DensityFunctions.ShiftA
//compute 取 blockX 与 blockZ 局部坐标 Y 置零用于 ShiftedNoise 的 X 偏移
public sealed class ShiftA : ShiftNoise
{
    public ShiftA(NoiseHolder offsetNoise) { OffsetNoise = offsetNoise; }

    public NoiseHolder OffsetNoise { get; }

    public double Compute(FunctionContext context)
        => ShiftNoise.SampleLocal(OffsetNoise, context.BlockX, 0.0, context.BlockZ);

    public DensityFunction MapChildren(Visitor visitor) => new ShiftA(visitor.VisitNoise(OffsetNoise));
}

//ShiftB ZX 交叉偏移噪声对应原版 DensityFunctions.ShiftB
//compute 取 blockZ 与 blockX 局部坐标 Y 置零用于 ShiftedNoise 的 Z 偏移
public sealed class ShiftB : ShiftNoise
{
    public ShiftB(NoiseHolder offsetNoise) { OffsetNoise = offsetNoise; }

    public NoiseHolder OffsetNoise { get; }

    public double Compute(FunctionContext context)
        => ShiftNoise.SampleLocal(OffsetNoise, context.BlockZ, context.BlockX, 0.0);

    public DensityFunction MapChildren(Visitor visitor) => new ShiftB(visitor.VisitNoise(OffsetNoise));
}

//Shift 三轴偏移噪声对应原版 DensityFunctions.Shift
//compute 取 blockX/Y/Z 全部局部坐标用于 ShiftedNoise 的 Y 偏移
public sealed class Shift : ShiftNoise
{
    public Shift(NoiseHolder offsetNoise) { OffsetNoise = offsetNoise; }

    public NoiseHolder OffsetNoise { get; }

    public double Compute(FunctionContext context)
        => ShiftNoise.SampleLocal(OffsetNoise, context.BlockX, context.BlockY, context.BlockZ);

    public DensityFunction MapChildren(Visitor visitor) => new Shift(visitor.VisitNoise(OffsetNoise));
}

//ShiftedNoise 偏移噪声密度函数对应原版 DensityFunctions.ShiftedNoise
//采样前先按 shiftX/Y/Z 三个密度函数计算坐标偏移再调用内部 Noise
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

    //MapChildren 替换噪声引用与三个偏移函数对应原版 mapChildren
    public DensityFunction MapChildren(Visitor visitor)
        => new ShiftedNoise(visitor.VisitNoise(NoiseData), XzScale, YScale,
            visitor.Apply(ShiftX), visitor.Apply(ShiftY), visitor.Apply(ShiftZ));

    public double MinValue => -NoiseData.MaxValue;
    public double MaxValue => NoiseData.MaxValue;
}
