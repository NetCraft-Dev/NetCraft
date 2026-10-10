using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Util;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen;

//NoiseChunk chunk noise context, maps to vanilla net.minecraft.world.level.levelgen.NoiseChunk
//At construction it wraps the density tree into interpolator/cache nodes by Marker type; generation advances cell by cell in a stream
//Subtrees wrapped in interpolated sample only at cell corners and interpolate trilinearly in the cell; other nodes evaluate per block
public sealed class NoiseChunk : FunctionContext, ContextProvider
{
    public ChunkAccess Chunk { get; }
    public RandomState RandomState { get; }
    public NoiseGeneratorSettings Settings { get; }
    public DensityFunction FullNoiseDensity { get; }
    public Aquifer Aquifer { get; }

    //Cell grid parameters come from NoiseSettings, matching the cell parameters in the vanilla NoiseChunk constructor
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

    //Wrapped nodes, one entry per node of the density tree. Measured at 4096 to 8191 entries per chunk, so a capacity of
    //256 was an order of magnitude short and only removed the first few doublings: the chain still resized through
    //2048 and 4096, and that entry array is what the allocation profile shows as Entry[DensityFunction, DensityFunction][]
    //8192 clears the measured range, so the array is built once
    private readonly Dictionary<DensityFunction, DensityFunction> _wrapped = new(8192);
    private readonly List<NoiseInterpolator> _interpolators = new();
    private readonly List<NoiseCacheAllInCell> _cellCaches = new();
    private readonly DensityFunction _preliminarySurfaceLevel;
    private readonly ContextProvider _sliceFillingContextProvider;
    //Preliminary surface cached per quart column; surface rules and the aquifer both query the same column repeatedly
    private readonly Dictionary<long, int> _preliminarySurfaceCache = new();

    //Reusable context for the preliminary surface sample below; the sampled function never retains it
    [ThreadStatic] private static SinglePointContext? _surfaceProbe;

    //beardifier structure terrain adjustment; when omitted a constant-zero marker is used so structures and terrain do not affect each other
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
        //Clamps the noise height settings to the chunk height, matches vanilla noiseSettings.clampToHeightAccessor(chunk)
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

        //Wrap the whole density tree into interpolator/cache nodes by Marker type
        var wrappedRouter = randomState.Router.MapAll(new WrapVisitor(this));
        _preliminarySurfaceLevel = wrappedRouter.PreliminarySurfaceLevel;
        Aquifer = CreateAquifer(settings, this, wrappedRouter, chunk.Pos, randomState,
            clampedMinY, clampedMaxY - clampedMinY);
        //The structure terrain adjustment is added to the final density; a placeholder marker constant 0 is used when no structures are passed
        FullNoiseDensity = Wrap(new MarkerNode(DensityFunctionsExtra.MarkerType.CacheAllInCell,
            new Ap2(Ap2.OpType.Add, wrappedRouter.FinalDensity, beardifier ?? BeardifierMarker.Instance)));
    }

    //CreateAquifer picks the full aquifer or the disabled one from settings.AquifersEnabled, matching the branch in the vanilla NoiseChunk constructor
    private static Aquifer CreateAquifer(NoiseGeneratorSettings settings, NoiseChunk noiseChunk,
        NoiseRouter router, ChunkPos pos, RandomState randomState, int minBlockY, int yBlockSize)
    {
        var globalFluidPicker = NoiseBasedChunkGenerator.CreateFluidPicker(settings);
        if (!settings.AquifersEnabled) return Aquifer.CreateDisabled(globalFluidPicker);
        return Aquifer.Create(noiseChunk, pos, router, randomState.AquiferRandom,
            minBlockY, yBlockSize, globalFluidPicker);
    }

    //Wrap builds the wrapper node for a Marker type, maps to vanilla NoiseChunk.wrap
    //Non-Marker nodes are returned as-is; their children were already wrapped recursively during the traversal
    private DensityFunction Wrap(DensityFunction function)
    {
        if (_wrapped.TryGetValue(function, out var cached)) return cached;
        var result = WrapNew(function);
        _wrapped[function] = result;
        NetCraft.Util.SiteCounters.Wrapped("noiseChunk", _wrapped.Count);
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
            //BlendDensity passes straight through in vanilla too when there is no blender
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

    //BlockX/Y/Z the current sample coordinate, the cell start plus the in-cell offset, maps to vanilla FunctionContext
    public int BlockX => _cellStartBlockX + _inCellX;
    public int BlockY => _cellStartBlockY + _inCellY;
    public int BlockZ => _cellStartBlockZ + _inCellZ;

    //ForIndex restores the three-axis offsets from the in-cell linear index, Y from top to bottom, maps to vanilla forIndex
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

    //FillAllDirectly walks the whole cell with Y descending and X/Z ascending, maps to vanilla fillAllDirectly
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

    //InitializeForFirstCellX starts the interpolation loop and fills the first X slice, maps to vanilla initializeForFirstCellX
    public void InitializeForFirstCellX()
    {
        if (_interpolating) throw new InvalidOperationException("Staring interpolation twice");
        _interpolating = true;
        _interpolationCounter = 0L;
        FillSlice(true, _firstCellX);
    }

    //AdvanceCellX moves to the next X cell and fills the next slice of corners, maps to vanilla advanceCellX
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

    //SelectCellYZ selects the current YZ cell, takes the eight corners and pre-fills the whole-cell caches, maps to vanilla selectCellYZ
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

    //GetInterpolatedState returns the block state of the current sample, maps to vanilla getInterpolatedState
    //The aquifer returns null when density > 0 and the caller falls back to Settings.DefaultBlock
    public BlockState? GetInterpolatedState()
    {
        var density = FullNoiseDensity.Compute(this);
        return Aquifer.ComputeSubstance(this, density);
    }

    //PreliminarySurfaceLevel preliminary surface height, maps to vanilla preliminarySurfaceLevel
    //Quantises to a quart-aligned coordinate by 4 blocks, then samples the preliminary_surface_level density function and floors it
    public int PreliminarySurfaceLevel(int blockX, int blockZ)
    {
        var quantizedX = blockX >> 2 << 2;
        var quantizedZ = blockZ >> 2 << 2;
        var key = ((long)quantizedX << 32) ^ (uint)quantizedZ;
        if (_preliminarySurfaceCache.TryGetValue(key, out var cached)) return cached;
        var value = Mth.Floor(
            _preliminarySurfaceLevel.Compute((_surfaceProbe ??= new SinglePointContext(0, 0, 0))
                .Set(quantizedX, 0, quantizedZ)));
        _preliminarySurfaceCache[key] = value;
        return value;
    }

    //MaxPreliminarySurfaceLevel takes the max preliminary surface over a block-coordinate rectangle at a 4-block step, maps to vanilla maxPreliminarySurfaceLevel
    //The aquifer uses it to give global fluid to cells above the surface instead of doing a centre search
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

    //WrapVisitor adapts NoiseChunk.Wrap into a density tree visitor
    private sealed class WrapVisitor : Visitor
    {
        private readonly NoiseChunk _chunk;

        public WrapVisitor(NoiseChunk chunk) => _chunk = chunk;

        public DensityFunction Apply(DensityFunction input) => _chunk.Wrap(input);

        public NoiseHolder VisitNoise(NoiseHolder noise) => noise;
    }

    //SliceFillingContextProvider slice filling context, maps to vanilla sliceFillingContextProvider
    //Sets the Y start from the cell's Y index so corner sampling lands on cell boundaries
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
