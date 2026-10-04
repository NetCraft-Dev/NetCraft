using NetCraft.Game.Util;
using NetCraft.Game.World.Level.LevelGen.Synth;
using NetCraft.Util;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen;

//DensityFunctionsExtra 密度函数扩展集对应原版 DensityFunctions 剩余嵌套类
//Spline/MarkerNode/RangeChoice/IntervalSelect/EndIslands/BlendAlpha/BlendOffset/Beardifier/FindTopSurface
//这些类在 NoiseRouterData 构建密度树时被广泛引用必须与原版行为对齐
public static class DensityFunctionsExtra
{
    //MarkerType 缓存标记类型对应原版 DensityFunctions.Marker.Type
    //标识密度函数在区块生成时的缓存策略Interpolated 结果跨格子插值其余控制缓存粒度
    public enum MarkerType
    {
        Interpolated,
        FlatCache,
        Cache2D,
        CacheOnce,
        CacheAllInCell,
        BlendDensity
    }

    //RangeChoice 范围选择工厂对应原版 DensityFunctions.rangeChoice
    public static RangeChoice RangeChoice(DensityFunction input, double minInclusive, double maxExclusive,
        DensityFunction whenInRange, DensityFunction whenOutOfRange)
        => new(input, minInclusive, maxExclusive, whenInRange, whenOutOfRange);

    //IntervalSelect 多段选择工厂对应原版 DensityFunctions.intervalSelect
    public static IntervalSelect IntervalSelect(DensityFunction input, double[] thresholds, DensityFunction[] functions)
        => new(input, thresholds, functions);

    //Interpolated 插值标记工厂对应原版 DensityFunctions.interpolated
    public static MarkerNode Interpolated(DensityFunction function) => new(MarkerType.Interpolated, function);

    //FlatCache 平面缓存标记工厂对应原版 DensityFunctions.flatCache
    public static MarkerNode FlatCache(DensityFunction function) => new(MarkerType.FlatCache, function);

    //Cache2D 二维缓存标记工厂对应原版 DensityFunctions.cache2d
    public static MarkerNode Cache2D(DensityFunction function) => new(MarkerType.Cache2D, function);

    //CacheOnce 单次缓存标记工厂对应原版 DensityFunctions.cacheOnce
    public static MarkerNode CacheOnce(DensityFunction function) => new(MarkerType.CacheOnce, function);

    //CacheAllInCell 全格缓存标记工厂对应原版 DensityFunctions.cacheAllInCell
    public static MarkerNode CacheAllInCell(DensityFunction function) => new(MarkerType.CacheAllInCell, function);

    //BlendDensity 混合密度标记工厂对应原版 DensityFunctions.blendDensity
    public static MarkerNode BlendDensity(DensityFunction function) => new(MarkerType.BlendDensity, function);

    //EndIslands 末地岛屿密度工厂对应原版 DensityFunctions.endIslands
    public static EndIslandDensityFunction EndIslands(long seed) => new(seed);

    //Spline 样条密度工厂对应原版 DensityFunctions.spline
    public static SplineFunction Spline(CubicSpline spline) => new(spline);
}

//MarkerNode 缓存标记节点对应原版 DensityFunctions.Marker
//包装 type 与 wrapped 子函数compute 委托 wrappedBlendDensity 的 min/max 返回无穷
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

//SplineFunction 样条密度函数对应原版 DensityFunctions.Spline
//包装 CubicSpline 为 DensityFunctioncompute 委托 spline.Sample
//简化设计直接持有 CubicSpline 不引入 Coordinate/Point 中间层因 CubicSpline 已直接接受 FunctionContext
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

    //mapChildren 委托 spline.mapCoordinates 把内部 DensityFunction 坐标替换为 visitor.apply 结果
    public DensityFunction MapChildren(Visitor visitor)
        => new SplineFunction(_spline.MapCoordinates(visitor.Apply));
}

//RangeChoice 范围选择密度函数对应原版 DensityFunctions.RangeChoice
//input 值落在 [minInclusive, maxExclusive) 时返回 whenInRange 否则返回 whenOutOfRange
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

//IntervalSelect 多段阈值选择密度函数对应原版 DensityFunctions.IntervalSelect
//input 值按 thresholds 升序分段选择对应 functionsthresholds 数量须为 functions 数量减一
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

//EndIslandDensityFunction 末地岛屿密度函数对应原版 DensityFunctions.EndIslandDensityFunction
//用 SimplexNoise 生成末地主岛与外围岛屿密度值compute 返回 (heightValue - 8) / 128
public sealed class EndIslandDensityFunction : SimpleFunction
{
    private const float IslandThreshold = -0.9f;

    //IslandChunkDistanceSqr 末地岛屿生成最小距离平方对应原版 NoiseRouterData.ISLAND_CHUNK_DISTANCE_SQR
    //步骤5 创建 NoiseRouterData 时改为引用共享常量此处暂用本地副本保持步骤3 自洽
    private const long IslandChunkDistanceSqr = 4096L;

    private readonly SimplexNoise _islandNoise;

    public EndIslandDensityFunction(long seed)
    {
        var islandRandom = new LegacyRandomSource(seed);
        islandRandom.ConsumeCount(17292);
        _islandNoise = new SimplexNoise(islandRandom);
    }

    //GetHeightValue 末地高度值对应原版 getHeightValue
    //以 sectionX/Z 八倍距离为基准衰减扫描周围 25x25 区块寻找岛屿叠加最大高度
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

//BlendAlpha 混合透明度密度函数对应原版 DensityFunctions.BlendAlpha
//固定返回 1.0 用于生物群系过渡权重
public sealed class BlendAlpha : SimpleFunction
{
    public static readonly BlendAlpha Instance = new();

    public double Compute(FunctionContext context) => 1.0;
    public double MinValue => 1.0;
    public double MaxValue => 1.0;
}

//BlendOffset 混合偏移密度函数对应原版 DensityFunctions.BlendOffset
//固定返回 0.0 用于生物群系过渡高度补偿
public sealed class BlendOffset : SimpleFunction
{
    public static readonly BlendOffset Instance = new();

    public double Compute(FunctionContext context) => 0.0;
    public double MinValue => 0.0;
    public double MaxValue => 0.0;
}

//BeardifierMarker 胡须化标记对应原版 DensityFunctions.BeardifierMarker
//占位密度函数实际胡须化由结构系统注入此处固定返回 0.0
public sealed class BeardifierMarker : SimpleFunction
{
    public static readonly BeardifierMarker Instance = new();

    public double Compute(FunctionContext context) => 0.0;
    public double MinValue => 0.0;
    public double MaxValue => 0.0;
}

//FindTopSurface 查找顶部表面密度函数对应原版 DensityFunctions.FindTopSurface
//从 upperBound 向下按 cellHeight 步长找首个 density > 0 的 Y 作为表面高度
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

    public double Compute(FunctionContext context)
    {
        var topY = Mth.Floor(UpperBound.Compute(context) / CellHeight) * CellHeight;
        if (topY <= LowerBound)
            return LowerBound;
        var i = topY;
        while (true)
        {
            var blockY = i;
            if (blockY < LowerBound)
                return LowerBound;
            var probe = new SinglePointContext(context.BlockX, blockY, context.BlockZ);
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
