using NetCraft.Codec;
using NetCraft.Game.World.Level.LevelGen.Synth;

namespace NetCraft.Game.World.Level.LevelGen;

//DensityFunction 密度函数接口对应原版 net.minecraft.world.level.levelgen.DensityFunction
//世界生成核心抽象按坐标采样密度值用于地形/洞穴/矿物分布决策
//子接口 SimpleFunction/Marker 区分简单常量与变换类
//FunctionContext 提供坐标访问 Visitor 用于子节点替换
//Codec 协变问题用每个子类提供 static readonly CodecInstance 字段解决不在此接口声明
public interface DensityFunction
{
    //Compute 按上下文坐标采样密度值
    double Compute(FunctionContext context);

    //FillArray 批量采样填充数组对应原版 fillArray
    void FillArray(double[] output, ContextProvider contextProvider);

    //MapChildren 替换子节点对应原版 mapChildren
    DensityFunction MapChildren(Visitor visitor);

    //MapAll 替换整个节点对应原版 mapAll
    //原版用一个递归包装 visitor 让节点里的 apply 调用也会展开子节点 只用 Apply(this) 会导致子树不被访问
    DensityFunction MapAll(Visitor visitor) => new RecursiveVisitor(visitor).Apply(this);

    double MinValue { get; }
    double MaxValue { get; }

    //Clamp 钳制包装对应原版 DensityFunction.clamp(min, max)
    //默认实现 new Clamp(this, min, max)子类可重写提供优化路径
    DensityFunction Clamp(double min, double max) => new Clamp(this, min, max);

    //Abs 绝对值包装对应原版 DensityFunction.abs
    DensityFunction Abs() => new MappedTypes.Abs(this);

    //Square 平方包装对应原版 DensityFunction.square
    DensityFunction Square() => new MappedTypes.Square(this);

    //Cube 三次方包装对应原版 DensityFunction.cube
    DensityFunction Cube() => new MappedTypes.Cube(this);

    //HalfNegative 负值减半包装对应原版 DensityFunction.halfNegative
    DensityFunction HalfNegative() => new MappedTypes.HalfNegative(this);

    //QuarterNegative 负值减四分之一包装对应原版 DensityFunction.quarterNegative
    DensityFunction QuarterNegative() => new MappedTypes.QuarterNegative(this);

    //Squeeze 挤压包装对应原版 DensityFunction.squeeze
    DensityFunction Squeeze() => new MappedTypes.Squeeze(this);

    //Invert 倒数包装对应原版 DensityFunction.invert
    DensityFunction Invert() => new MappedTypes.Invert(this);
}

//FunctionContext 函数上下文提供采样坐标对应原版 DensityFunction.FunctionContext
public interface FunctionContext
{
    int BlockX { get; }
    int BlockY { get; }
    int BlockZ { get; }
}

//ContextProvider 上下文提供者按索引产生 FunctionContext 对应原版 DensityFunction.ContextProvider
public interface ContextProvider
{
    FunctionContext ForIndex(int index);
    void FillAllDirectly(double[] output, DensityFunction function);
}

//SimpleFunction 简单函数接口对应原版 DensityFunction.SimpleFunction
//不依赖上下文坐标的常量与变换类实现 fillArray/mapChildren 默认行为
public interface SimpleFunction : DensityFunction
{
    //FillArray 简单函数直接批量填充避免逐坐标采样
    void DensityFunction.FillArray(double[] output, ContextProvider contextProvider)
        => contextProvider.FillAllDirectly(output, this);

    //MapChildren 简单函数无子节点返回自身
    DensityFunction DensityFunction.MapChildren(Visitor visitor) => this;
}

//Visitor 访问者接口对应原版 DensityFunction.Visitor
//apply 替换节点 visitNoise 替换噪声引用
public interface Visitor
{
    DensityFunction Apply(DensityFunction input);

    //VisitNoise 替换噪声引用默认返回原值
    NoiseHolder VisitNoise(NoiseHolder noise) => noise;
}

//RecursiveVisitor 递归包装访问者对应原版 mapAll 内部的匿名 RecursiveVisitor
//把 Apply 变成先展开子节点再交给真实 visitor 保证整棵密度树都被访问到
internal sealed class RecursiveVisitor : Visitor
{
    private readonly Visitor _inner;

    public RecursiveVisitor(Visitor inner) => _inner = inner;

    public DensityFunction Apply(DensityFunction input) => _inner.Apply(input.MapChildren(this));

    public NoiseHolder VisitNoise(NoiseHolder noise) => _inner.VisitNoise(noise);
}

//SinglePointContext 单点上下文对应原版 DensityFunction.SinglePointContext
//可变: 逐格采样的调用方复用一个实例 每格 new 一个在水合层那种近十万格的循环里是纯浪费
//前提是复用方独占该实例且被调用的密度函数不把上下文留存下来 这两点由复用方保证
public sealed class SinglePointContext : FunctionContext
{
    public int BlockX { get; private set; }
    public int BlockY { get; private set; }
    public int BlockZ { get; private set; }

    public SinglePointContext(int blockX, int blockY, int blockZ) => Set(blockX, blockY, blockZ);

    //At 静态工厂按坐标构造 需要独立实例的调用方用它
    public static SinglePointContext At(int x, int y, int z) => new(x, y, z);

    //Set 改写坐标供复用 返回自身方便连着调
    public SinglePointContext Set(int x, int y, int z)
    {
        BlockX = x;
        BlockY = y;
        BlockZ = z;
        return this;
    }
}

//NoiseHolder 噪声持有者对应原版 DensityFunction.NoiseHolder
//包装 NoiseParameters 数据与可选 NormalNoise 实例数据反序列化时 noise 为 null
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

    //GetValue 按坐标采样若 Noise 为 null 返回 0 对应原版空数据兜底
    public double GetValue(double x, double y, double z)
        => Noise?.GetValue(x, y, z) ?? 0.0;

    //MaxValue 包装噪声最大值若 Noise 为 null 返回 2.0 兜底
    public double MaxValue => Noise?.MaxValue ?? 2.0;
}
