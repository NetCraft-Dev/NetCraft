using NetCraft.Util;

namespace NetCraft.Game.World.Level.LevelGen;

//INoiseChunkDensity 区块级密度函数接口对应原版 NoiseChunk.NoiseChunkDensityFunction
//表示由 NoiseChunk.wrap 按 Marker 类型生成的包装节点内部保留原始子函数
public interface INoiseChunkDensity
{
    DensityFunction Wrapped { get; }
}

//NoiseInterpolator 插值密度函数对应原版 NoiseChunk.NoiseInterpolator
//只有被 interpolated 包裹的子树才走 cell 角点采样 + 三线性插值其余节点逐方块求值
public sealed class NoiseInterpolator : DensityFunction, INoiseChunkDensity
{
    private readonly NoiseChunk _chunk;
    private readonly DensityFunction _noiseFiller;

    //slice0/slice1 相邻两个 X 切片的角点值 selectCellYZ 一次性取 8 个角点
    public double[][] Slice0 { get; private set; }
    public double[][] Slice1 { get; private set; }

    private double _noise000, _noise001, _noise100, _noise101;
    private double _noise010, _noise011, _noise110, _noise111;
    private double _valueXZ00, _valueXZ10, _valueXZ01, _valueXZ11;
    private double _valueZ0, _valueZ1, _value;

    public NoiseInterpolator(NoiseChunk chunk, DensityFunction noiseFiller)
    {
        _chunk = chunk;
        _noiseFiller = noiseFiller;
        Slice0 = AllocateSlice(chunk.CellCountY, chunk.CellCountXZ);
        Slice1 = AllocateSlice(chunk.CellCountY, chunk.CellCountXZ);
        chunk.RegisterInterpolator(this);
    }

    private static double[][] AllocateSlice(int cellCountY, int cellCountZ)
    {
        var sizeZ = cellCountZ + 1;
        var sizeY = cellCountY + 1;
        var result = new double[sizeZ][];
        for (var cellZIndex = 0; cellZIndex < sizeZ; cellZIndex++)
            result[cellZIndex] = new double[sizeY];
        return result;
    }

    public void SelectCellYZ(int cellYIndex, int cellZIndex)
    {
        _noise000 = Slice0[cellZIndex][cellYIndex];
        _noise001 = Slice0[cellZIndex + 1][cellYIndex];
        _noise100 = Slice1[cellZIndex][cellYIndex];
        _noise101 = Slice1[cellZIndex + 1][cellYIndex];
        _noise010 = Slice0[cellZIndex][cellYIndex + 1];
        _noise011 = Slice0[cellZIndex + 1][cellYIndex + 1];
        _noise110 = Slice1[cellZIndex][cellYIndex + 1];
        _noise111 = Slice1[cellZIndex + 1][cellYIndex + 1];
    }

    public void UpdateForY(double factorY)
    {
        _valueXZ00 = Mth.Lerp(factorY, _noise000, _noise010);
        _valueXZ10 = Mth.Lerp(factorY, _noise100, _noise110);
        _valueXZ01 = Mth.Lerp(factorY, _noise001, _noise011);
        _valueXZ11 = Mth.Lerp(factorY, _noise101, _noise111);
    }

    public void UpdateForX(double factorX)
    {
        _valueZ0 = Mth.Lerp(factorX, _valueXZ00, _valueXZ10);
        _valueZ1 = Mth.Lerp(factorX, _valueXZ01, _valueXZ11);
    }

    public void UpdateForZ(double factorZ) => _value = Mth.Lerp(factorZ, _valueZ0, _valueZ1);

    public void SwapSlices()
    {
        (Slice0, Slice1) = (Slice1, Slice0);
    }

    public double Compute(FunctionContext context)
    {
        if (!ReferenceEquals(context, _chunk)) return _noiseFiller.Compute(context);
        if (!_chunk.Interpolating)
            throw new InvalidOperationException("Trying to sample interpolator outside the interpolation loop");
        if (_chunk.FillingCell)
        {
            return Mth.Lerp3(_chunk.InCellX / (double)_chunk.CellWidth, _chunk.InCellY / (double)_chunk.CellHeight,
                _chunk.InCellZ / (double)_chunk.CellWidth,
                _noise000, _noise100, _noise010, _noise110, _noise001, _noise101, _noise011, _noise111);
        }
        return _value;
    }

    public void FillArray(double[] output, ContextProvider contextProvider)
    {
        if (_chunk.FillingCell) contextProvider.FillAllDirectly(output, this);
        else _noiseFiller.FillArray(output, contextProvider);
    }

    public DensityFunction MapChildren(Visitor visitor) => this;

    public DensityFunction Wrapped => _noiseFiller;

    public double MinValue => _noiseFiller.MinValue;
    public double MaxValue => _noiseFiller.MaxValue;
}

//NoiseFlatCache 平面缓存对应原版 NoiseChunk.FlatCache
//按 quart 列预填整块噪声表取坐标时命中表内直接查表
public sealed class NoiseFlatCache : DensityFunction, INoiseChunkDensity
{
    private readonly NoiseChunk _chunk;
    private readonly DensityFunction _noiseFiller;
    private readonly double[] _values;
    private readonly int _sizeXZ;

    public NoiseFlatCache(NoiseChunk chunk, DensityFunction noiseFiller, bool fill)
    {
        _chunk = chunk;
        _noiseFiller = noiseFiller;
        _sizeXZ = chunk.NoiseSizeXZ + 1;
        _values = new double[_sizeXZ * _sizeXZ];
        if (!fill) return;
        for (var x = 0; x <= chunk.NoiseSizeXZ; x++)
        {
            var blockX = (chunk.FirstNoiseX + x) << 2;
            for (var z = 0; z <= chunk.NoiseSizeXZ; z++)
            {
                var blockZ = (chunk.FirstNoiseZ + z) << 2;
                _values[x + z * _sizeXZ] = noiseFiller.Compute(SinglePointContext.At(blockX, 0, blockZ));
            }
        }
    }

    public double Compute(FunctionContext context)
    {
        var x = (context.BlockX >> 2) - _chunk.FirstNoiseX;
        var z = (context.BlockZ >> 2) - _chunk.FirstNoiseZ;
        if (x >= 0 && z >= 0 && x < _sizeXZ && z < _sizeXZ) return _values[x + z * _sizeXZ];
        return _noiseFiller.Compute(context);
    }

    public void FillArray(double[] output, ContextProvider contextProvider)
        => contextProvider.FillAllDirectly(output, this);

    public DensityFunction MapChildren(Visitor visitor) => this;

    public DensityFunction Wrapped => _noiseFiller;

    public double MinValue => _noiseFiller.MinValue;
    public double MaxValue => _noiseFiller.MaxValue;
}

//NoiseCache2D 二维缓存对应原版 NoiseChunk.Cache2D
//只记上一次查询的列坐标与值同列连续查询直接复用
public sealed class NoiseCache2D : DensityFunction, INoiseChunkDensity
{
    private const long InvalidPos = long.MinValue;

    private readonly DensityFunction _function;
    private long _lastPos2D = InvalidPos;
    private double _lastValue;

    public NoiseCache2D(DensityFunction function) => _function = function;

    public double Compute(FunctionContext context)
    {
        var pos2D = ((context.BlockX & 0xFFFFFFFFL) | ((long)context.BlockZ << 32));
        if (_lastPos2D == pos2D) return _lastValue;
        _lastPos2D = pos2D;
        _lastValue = _function.Compute(context);
        return _lastValue;
    }

    public void FillArray(double[] output, ContextProvider contextProvider)
        => _function.FillArray(output, contextProvider);

    public DensityFunction MapChildren(Visitor visitor) => this;

    public DensityFunction Wrapped => _function;

    public double MinValue => _function.MinValue;
    public double MaxValue => _function.MaxValue;
}

//NoiseCacheOnce 单次缓存对应原版 NoiseChunk.CacheOnce
//按插值计数器缓存同一坐标的单点值按数组计数器缓存整块数组
public sealed class NoiseCacheOnce : DensityFunction, INoiseChunkDensity
{
    private readonly NoiseChunk _chunk;
    private readonly DensityFunction _function;
    private long _lastCounter;
    private long _lastArrayCounter;
    private double _lastValue;
    private double[]? _lastArray;

    public NoiseCacheOnce(NoiseChunk chunk, DensityFunction function)
    {
        _chunk = chunk;
        _function = function;
    }

    public double Compute(FunctionContext context)
    {
        if (!ReferenceEquals(context, _chunk)) return _function.Compute(context);
        if (_lastArray is not null && _lastArrayCounter == _chunk.ArrayInterpolationCounter)
            return _lastArray[_chunk.ArrayIndex];
        if (_lastCounter == _chunk.InterpolationCounter) return _lastValue;
        _lastCounter = _chunk.InterpolationCounter;
        _lastValue = _function.Compute(context);
        return _lastValue;
    }

    public void FillArray(double[] output, ContextProvider contextProvider)
    {
        if (_lastArray is not null && _lastArrayCounter == _chunk.ArrayInterpolationCounter)
        {
            Array.Copy(_lastArray, output, output.Length);
            return;
        }
        _function.FillArray(output, contextProvider);
        if (_lastArray is not null && _lastArray.Length == output.Length) Array.Copy(output, _lastArray, output.Length);
        else _lastArray = (double[])output.Clone();
        _lastArrayCounter = _chunk.ArrayInterpolationCounter;
    }

    public DensityFunction MapChildren(Visitor visitor) => this;

    public DensityFunction Wrapped => _function;

    public double MinValue => _function.MinValue;
    public double MaxValue => _function.MaxValue;
}

//NoiseCacheAllInCell 整格缓存对应原版 NoiseChunk.CacheAllInCell
//每个 cell 开始前把格内所有方块的值算一次之后按格内偏移直接查表
public sealed class NoiseCacheAllInCell : DensityFunction, INoiseChunkDensity
{
    private readonly NoiseChunk _chunk;
    private readonly DensityFunction _noiseFiller;
    private readonly double[] _values;

    public NoiseCacheAllInCell(NoiseChunk chunk, DensityFunction noiseFiller)
    {
        _chunk = chunk;
        _noiseFiller = noiseFiller;
        _values = new double[chunk.CellWidth * chunk.CellWidth * chunk.CellHeight];
        chunk.RegisterCellCache(this);
    }

    //Fill 在当前 cell 选中后预填整格对应原版 selectCellYZ 里的 fillArray
    public void Fill() => _chunk.FillAllDirectly(_values, _noiseFiller);

    public double Compute(FunctionContext context)
    {
        if (!ReferenceEquals(context, _chunk)) return _noiseFiller.Compute(context);
        if (!_chunk.Interpolating)
            throw new InvalidOperationException("Trying to sample interpolator outside the interpolation loop");
        var x = _chunk.InCellX;
        var y = _chunk.InCellY;
        var z = _chunk.InCellZ;
        if (x >= 0 && y >= 0 && z >= 0
            && x < _chunk.CellWidth && y < _chunk.CellHeight && z < _chunk.CellWidth)
            return _values[(_chunk.CellHeight - 1 - y) * _chunk.CellWidth * _chunk.CellWidth
                + x * _chunk.CellWidth + z];
        return _noiseFiller.Compute(context);
    }

    public void FillArray(double[] output, ContextProvider contextProvider)
        => contextProvider.FillAllDirectly(output, this);

    public DensityFunction MapChildren(Visitor visitor) => this;

    public DensityFunction Wrapped => _noiseFiller;

    public double MinValue => _noiseFiller.MinValue;
    public double MaxValue => _noiseFiller.MaxValue;
}
