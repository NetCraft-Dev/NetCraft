using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Util;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen;

//NoiseChunk 区块噪声上下文对应原版 net.minecraft.world.level.levelgen.NoiseChunk
//构造时按 Marker 类型把密度树包装成插值/缓存节点 生成时逐 cell 流式推进
//被 interpolated 包裹的子树只在 cell 角点采样格内走三线性插值其余节点逐方块求值
public sealed class NoiseChunk : FunctionContext, ContextProvider
{
    public ChunkAccess Chunk { get; }
    public RandomState RandomState { get; }
    public NoiseGeneratorSettings Settings { get; }
    public DensityFunction FullNoiseDensity { get; }
    public Aquifer Aquifer { get; }

    //cell 网格参数来自 NoiseSettings 对应原版 NoiseChunk 构造中的 cell 参数
    public int CellWidth { get; }
    public int CellHeight { get; }
    public int CellCountXZ { get; }
    public int CellCountY { get; }
    public int CellNoiseMinY { get; }
    public int FirstNoiseX { get; }
    public int FirstNoiseZ { get; }
    public int NoiseSizeXZ { get; }

    private readonly int _firstCellX;
    private readonly int _firstCellZ;
    private int _cellStartBlockX;
    private int _cellStartBlockY;
    private int _cellStartBlockZ;
    private int _inCellX;
    private int _inCellY;
    private int _inCellZ;
    private int _arrayIndex;
    private long _interpolationCounter;
    private long _arrayInterpolationCounter;
    private bool _interpolating;
    private bool _fillingCell;

    private readonly Dictionary<DensityFunction, DensityFunction> _wrapped = new();
    private readonly List<NoiseInterpolator> _interpolators = new();
    private readonly List<NoiseCacheAllInCell> _cellCaches = new();
    private readonly DensityFunction _preliminarySurfaceLevel;
    private readonly ContextProvider _sliceFillingContextProvider;
    //初步地表按 quart 列缓存 地表规则与含水层都会反复问同一列
    private readonly Dictionary<long, int> _preliminarySurfaceCache = new();

    //beardifier 结构地形适配项 未传时用恒为 0 的标记 结构与地形互不影响
    public NoiseChunk(ChunkAccess chunk, RandomState randomState, NoiseGeneratorSettings settings,
        DensityFunction? beardifier = null)
    {
        Chunk = chunk;
        RandomState = randomState;
        Settings = settings;

        var noiseSettings = settings.NoiseSettings;
        CellWidth = noiseSettings.GetCellWidth();
        CellHeight = noiseSettings.GetCellHeight();
        CellCountXZ = 16 / CellWidth;
        //按区块高度夹取噪声高度设置 对应原版 noiseSettings.clampToHeightAccessor(chunk)
        var levelMinY = chunk.MinSectionY * 16;
        var levelMaxY = levelMinY + chunk.SectionsCount * 16;
        var clampedMinY = Math.Max(noiseSettings.MinY, levelMinY);
        var clampedMaxY = Math.Min(noiseSettings.MinY + noiseSettings.Height, levelMaxY);
        CellCountY = Mth.FloorDiv(clampedMaxY - clampedMinY, CellHeight);
        CellNoiseMinY = Mth.FloorDiv(clampedMinY, CellHeight);

        var chunkMinBlockX = chunk.Pos.MinBlockX;
        var chunkMinBlockZ = chunk.Pos.MinBlockZ;
        _firstCellX = Mth.FloorDiv(chunkMinBlockX, CellWidth);
        _firstCellZ = Mth.FloorDiv(chunkMinBlockZ, CellWidth);
        FirstNoiseX = chunkMinBlockX >> 2;
        FirstNoiseZ = chunkMinBlockZ >> 2;
        NoiseSizeXZ = CellCountXZ * CellWidth >> 2;
        _sliceFillingContextProvider = new SliceFillingContextProvider(this);

        //整棵密度树按 Marker 类型包装插值/缓存节点
        var wrappedRouter = randomState.Router.MapAll(new WrapVisitor(this));
        _preliminarySurfaceLevel = wrappedRouter.PreliminarySurfaceLevel;
        Aquifer = CreateAquifer(settings, this, wrappedRouter, chunk.Pos, randomState,
            clampedMinY, clampedMaxY - clampedMinY);
        //结构地形适配项加在最终密度上 没传结构时用占位标记恒为 0
        FullNoiseDensity = Wrap(new MarkerNode(DensityFunctionsExtra.MarkerType.CacheAllInCell,
            new Ap2(Ap2.OpType.Add, wrappedRouter.FinalDensity, beardifier ?? BeardifierMarker.Instance)));
    }

    //CreateAquifer 按 settings.AquifersEnabled 决定使用完整含水层还是禁用版对应原版 NoiseChunk 构造中的分支
    private static Aquifer CreateAquifer(NoiseGeneratorSettings settings, NoiseChunk noiseChunk,
        NoiseRouter router, ChunkPos pos, RandomState randomState, int minBlockY, int yBlockSize)
    {
        var globalFluidPicker = NoiseBasedChunkGenerator.CreateFluidPicker(settings);
        if (!settings.AquifersEnabled) return Aquifer.CreateDisabled(globalFluidPicker);
        return Aquifer.Create(noiseChunk, pos, router, randomState.AquiferRandom,
            minBlockY, yBlockSize, globalFluidPicker);
    }

    //Wrap 按 Marker 类型生成包装节点对应原版 NoiseChunk.wrap
    //非 Marker 节点原样返回其子节点已在遍历时递归包装好
    private DensityFunction Wrap(DensityFunction function)
    {
        if (_wrapped.TryGetValue(function, out var cached)) return cached;
        var result = WrapNew(function);
        _wrapped[function] = result;
        return result;
    }

    private DensityFunction WrapNew(DensityFunction function) => function switch
    {
        MarkerNode marker => marker.Type switch
        {
            DensityFunctionsExtra.MarkerType.Interpolated => new NoiseInterpolator(this, marker.Wrapped),
            DensityFunctionsExtra.MarkerType.FlatCache => new NoiseFlatCache(this, marker.Wrapped, true),
            DensityFunctionsExtra.MarkerType.Cache2D => new NoiseCache2D(marker.Wrapped),
            DensityFunctionsExtra.MarkerType.CacheOnce => new NoiseCacheOnce(this, marker.Wrapped),
            DensityFunctionsExtra.MarkerType.CacheAllInCell => new NoiseCacheAllInCell(this, marker.Wrapped),
            //BlendDensity 在没有 blender 时原版也直接透传
            _ => marker.Wrapped
        },
        HolderHolder holder => holder.Function,
        _ => function
    };

    internal void RegisterInterpolator(NoiseInterpolator interpolator) => _interpolators.Add(interpolator);

    internal void RegisterCellCache(NoiseCacheAllInCell cache) => _cellCaches.Add(cache);

    internal bool Interpolating => _interpolating;
    internal bool FillingCell => _fillingCell;
    internal int InCellX => _inCellX;
    internal int InCellY => _inCellY;
    internal int InCellZ => _inCellZ;
    internal int ArrayIndex => _arrayIndex;
    internal long InterpolationCounter => _interpolationCounter;
    internal long ArrayInterpolationCounter => _arrayInterpolationCounter;

    //BlockX/Y/Z 当前采样点坐标由 cell 起点加格内偏移组成对应原版 FunctionContext
    public int BlockX => _cellStartBlockX + _inCellX;
    public int BlockY => _cellStartBlockY + _inCellY;
    public int BlockZ => _cellStartBlockZ + _inCellZ;

    //ForIndex 按格内线性下标还原三轴偏移 Y 从上往下对应原版 forIndex
    public FunctionContext ForIndex(int cellIndex)
    {
        var zInCell = cellIndex % CellWidth;
        var xyIndex = cellIndex / CellWidth;
        var xInCell = xyIndex % CellWidth;
        var yInCell = CellHeight - 1 - xyIndex / CellWidth;
        _inCellX = xInCell;
        _inCellY = yInCell;
        _inCellZ = zInCell;
        _arrayIndex = cellIndex;
        return this;
    }

    //FillAllDirectly 按 Y 降序 X/Z 升序遍历整格对应原版 fillAllDirectly
    public void FillAllDirectly(double[] output, DensityFunction function)
    {
        _arrayIndex = 0;
        for (var yInCell = CellHeight - 1; yInCell >= 0; yInCell--)
        {
            _inCellY = yInCell;
            for (var xInCell = 0; xInCell < CellWidth; xInCell++)
            {
                _inCellX = xInCell;
                for (var zInCell = 0; zInCell < CellWidth; zInCell++)
                {
                    _inCellZ = zInCell;
                    output[_arrayIndex++] = function.Compute(this);
                }
            }
        }
    }

    //InitializeForFirstCellX 开始插值循环并填第一个 X 切片对应原版 initializeForFirstCellX
    public void InitializeForFirstCellX()
    {
        if (_interpolating) throw new InvalidOperationException("Staring interpolation twice");
        _interpolating = true;
        _interpolationCounter = 0L;
        FillSlice(true, _firstCellX);
    }

    //AdvanceCellX 推进到下一个 X cell 填下一片角点对应原版 advanceCellX
    public void AdvanceCellX(int cellXIndex)
    {
        FillSlice(false, _firstCellX + cellXIndex + 1);
        _cellStartBlockX = (_firstCellX + cellXIndex) * CellWidth;
    }

    private void FillSlice(bool slice0, int cellX)
    {
        _cellStartBlockX = cellX * CellWidth;
        _inCellX = 0;
        for (var cellZIndex = 0; cellZIndex < CellCountXZ + 1; cellZIndex++)
        {
            var cellZ = _firstCellZ + cellZIndex;
            _cellStartBlockZ = cellZ * CellWidth;
            _inCellZ = 0;
            _arrayInterpolationCounter++;
            foreach (var interpolator in _interpolators)
            {
                var slice = slice0 ? interpolator.Slice0[cellZIndex] : interpolator.Slice1[cellZIndex];
                interpolator.FillArray(slice, _sliceFillingContextProvider);
            }
        }
        _arrayInterpolationCounter++;
    }

    //SelectCellYZ 选中当前 YZ cell 取八个角点并预填整格缓存对应原版 selectCellYZ
    public void SelectCellYZ(int cellYIndex, int cellZIndex)
    {
        foreach (var interpolator in _interpolators) interpolator.SelectCellYZ(cellYIndex, cellZIndex);
        _fillingCell = true;
        _cellStartBlockY = (cellYIndex + CellNoiseMinY) * CellHeight;
        _cellStartBlockZ = (_firstCellZ + cellZIndex) * CellWidth;
        _arrayInterpolationCounter++;
        foreach (var cellCache in _cellCaches) cellCache.Fill();
        _arrayInterpolationCounter++;
        _fillingCell = false;
    }

    public void UpdateForY(int posY, double factorY)
    {
        _inCellY = posY - _cellStartBlockY;
        foreach (var interpolator in _interpolators) interpolator.UpdateForY(factorY);
    }

    public void UpdateForX(int posX, double factorX)
    {
        _inCellX = posX - _cellStartBlockX;
        foreach (var interpolator in _interpolators) interpolator.UpdateForX(factorX);
    }

    public void UpdateForZ(int posZ, double factorZ)
    {
        _inCellZ = posZ - _cellStartBlockZ;
        _interpolationCounter++;
        foreach (var interpolator in _interpolators) interpolator.UpdateForZ(factorZ);
    }

    public void SwapSlices()
    {
        foreach (var interpolator in _interpolators) interpolator.SwapSlices();
    }

    public void StopInterpolation()
    {
        if (!_interpolating) throw new InvalidOperationException("Staring interpolation twice");
        _interpolating = false;
    }

    //GetInterpolatedState 取当前采样点的方块状态对应原版 getInterpolatedState
    //密度>0 时含水层返回 null 由上层用 Settings.DefaultBlock 兜底
    public BlockState? GetInterpolatedState()
    {
        var density = FullNoiseDensity.Compute(this);
        return Aquifer.ComputeSubstance(this, density);
    }

    //PreliminarySurfaceLevel 初步地表高度对应原版 preliminarySurfaceLevel
    //先按 4 格量化到 quart 对齐坐标 再采样 preliminary_surface_level 密度函数并向下取整
    public int PreliminarySurfaceLevel(int blockX, int blockZ)
    {
        var quantizedX = blockX >> 2 << 2;
        var quantizedZ = blockZ >> 2 << 2;
        var key = ((long)quantizedX << 32) ^ (uint)quantizedZ;
        if (_preliminarySurfaceCache.TryGetValue(key, out var cached)) return cached;
        var value = Mth.Floor(
            _preliminarySurfaceLevel.Compute(SinglePointContext.At(quantizedX, 0, quantizedZ)));
        _preliminarySurfaceCache[key] = value;
        return value;
    }

    //MaxPreliminarySurfaceLevel 在方块坐标矩形内按 4 格步长取初步地表最大值对应原版 maxPreliminarySurfaceLevel
    //含水层用它决定高于地表的网格不再做中心搜索直接给全局流体
    public int MaxPreliminarySurfaceLevel(int minBlockX, int minBlockZ, int maxBlockX, int maxBlockZ)
    {
        var maxY = int.MinValue;
        for (var blockZ = minBlockZ; blockZ <= maxBlockZ; blockZ += 4)
        {
            for (var blockX = minBlockX; blockX <= maxBlockX; blockX += 4)
            {
                var surfaceLevel = PreliminarySurfaceLevel(blockX, blockZ);
                if (surfaceLevel > maxY) maxY = surfaceLevel;
            }
        }
        return maxY;
    }

    //WrapVisitor 把 NoiseChunk.Wrap 包装成密度树访问者
    private sealed class WrapVisitor : Visitor
    {
        private readonly NoiseChunk _chunk;

        public WrapVisitor(NoiseChunk chunk) => _chunk = chunk;

        public DensityFunction Apply(DensityFunction input) => _chunk.Wrap(input);

        public NoiseHolder VisitNoise(NoiseHolder noise) => noise;
    }

    //SliceFillingContextProvider 切片填充上下文对应原版 sliceFillingContextProvider
    //按 cell 的 Y 序号设置 Y 起点让角点采样落在 cell 边界上
    private sealed class SliceFillingContextProvider : ContextProvider
    {
        private readonly NoiseChunk _chunk;

        public SliceFillingContextProvider(NoiseChunk chunk) => _chunk = chunk;

        public FunctionContext ForIndex(int cellYIndex)
        {
            _chunk._cellStartBlockY = (cellYIndex + _chunk.CellNoiseMinY) * _chunk.CellHeight;
            _chunk._interpolationCounter++;
            _chunk._inCellY = 0;
            _chunk._arrayIndex = cellYIndex;
            return _chunk;
        }

        public void FillAllDirectly(double[] output, DensityFunction function)
        {
            for (var cellYIndex = 0; cellYIndex < _chunk.CellCountY + 1; cellYIndex++)
            {
                _chunk._cellStartBlockY = (cellYIndex + _chunk.CellNoiseMinY) * _chunk.CellHeight;
                _chunk._interpolationCounter++;
                _chunk._inCellY = 0;
                _chunk._arrayIndex = cellYIndex;
                output[cellYIndex] = function.Compute(_chunk);
            }
        }
    }
}
