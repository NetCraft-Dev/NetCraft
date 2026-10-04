using NetCraft.Util;
using NetCraft.Game.World.Level.LevelGen;

namespace NetCraft.Game.Util;

//CubicSpline 三次样条插值对应原版 net.minecraft.util.CubicSpline<I>
//原版用泛型 I extends BoundedFloatFunction<?> 此处简化直接持有 DensityFunction 作为坐标
//TerrainProvider 用 builder 模式构造层级 spline 树每个节点 Sample 按 FunctionContext 取坐标值
//区间内 Hermite 三次插值端点用 linearExtend 外推对齐原版 Multipoint.sample
public interface CubicSpline
{
    float Sample(FunctionContext context);
    float MinValue { get; }
    float MaxValue { get; }
    CubicSpline MapCoordinates(Func<DensityFunction, DensityFunction> mapper);

    //Constant 工厂对应原版 CubicSpline.constant
    static CubicSpline Constant(float value) => new CubicSplineConstant(value);

    //Builder 工厂对应原版 CubicSpline.builder(coordinate)
    static CubicSplineBuilder Builder(DensityFunction coordinate)
        => new(coordinate, v => v);

    //Builder 工厂带 valueTransformer 对应原版 CubicSpline.builder(coordinate, valueTransformer)
    //valueTransformer 对 addPoint 的 value 做变换amplified 模式用此放大 offset
    static CubicSplineBuilder Builder(DensityFunction coordinate, Func<float, float> valueTransformer)
        => new(coordinate, valueTransformer);
}

//Multipoint 多点样条对应原版 CubicSpline.Multipoint
//持有 coordinate/locations/values/derivatives 构造时计算 min/max 边界
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
        //区间内 d1/d2 非零时考虑 Hermite 三次项的极值边界对齐原版
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

    //Coordinate/Points 暴露节点数据供 Codec 序列化
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

    //LinearExtend 端点线性外推对应原版 Multipoint.linearExtend
    //derivative 为 0 时返回原值否则按斜率外推
    private static float LinearExtend(float input, float[] locations, float value, float[] derivatives, int index)
    {
        var derivative = derivatives[index];
        if (derivative == 0.0f)
            return value;
        return value + derivative * (input - locations[index]);
    }

    //FindIntervalStart 二分查找 input 所属区间起点对应原版 findIntervalStart
    //返回 -1 表示 input 小于所有 locations返回 lastIndex 表示 input 大于等于最后一个
    //原版写的是 lambda 但 Java 的 lambda 能内联 .NET 的 Func<int,bool> 每次调用要分配捕获闭包
    //二分过程还每轮走一次委托调用 逐格采样的路径上这是纯开销 这里直接展开成循环
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

//CubicSplineConstant 常量样条对应原版 CubicSpline.Constant
//所有坐标返回固定 value min/max 等于 value
public sealed class CubicSplineConstant : CubicSpline
{
    private readonly float _value;
    public CubicSplineConstant(float value) { _value = value; }
    public float Sample(FunctionContext context) => _value;
    public float MinValue => _value;
    public float MaxValue => _value;

    //Value 暴露常量值供 Codec 序列化
    public float Value => _value;

    public CubicSpline MapCoordinates(Func<DensityFunction, DensityFunction> mapper) => this;
}

//CubicSplineBuilder 样条构造器对应原版 CubicSpline.Builder
//addPoint 必须升序注册 build 时构造 Multipoint
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

    //AddPoint 带斜率重载供 Codec 还原 JSON 里的 derivative
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
