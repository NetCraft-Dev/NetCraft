using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Util;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen;

//Aquifer aquifer interface, maps to vanilla net.minecraft.world.level.levelgen.Aquifer
//Decides the block state of each cell from the NoiseRouter's Barrier/FluidLevelFloodedness/FluidLevelSpread/Lava and a globalFluidPicker
//Two implementations: NoiseBasedAquifer the full aquifer, and DisabledAquifer the simplified one returning null when density > 0 and the global fluid otherwise
public interface Aquifer
{
    //FluidPicker returns a FluidStatus by coordinate, maps to vanilla Aquifer.FluidPicker
    public interface FluidPicker
    {
        FluidStatus ComputeFluid(int blockX, int blockY, int blockZ);
    }

    //ComputeSubstance returns the block state from the context and density; density > 0 usually returns null and the caller falls back to DefaultBlock
    BlockState? ComputeSubstance(FunctionContext context, double density);

    //ShouldScheduleFluidUpdate whether a fluid update should be scheduled, maps to vanilla shouldScheduleFluidUpdate
    bool ShouldScheduleFluidUpdate();

    //Create full aquifer factory, maps to vanilla create
    //In phase P0 NoiseBasedAquifer delegates to DisabledAquifer internally; the constructor signature is kept so P1 can complete the aquifer grid algorithm
    static Aquifer Create(NoiseChunk noiseChunk, ChunkPos pos, NoiseRouter router,
        PositionalRandomFactory positionalRandomFactory, int minBlockY, int yBlockSize,
        FluidPicker globalFluidPicker)
        => new NoiseBasedAquifer(noiseChunk, pos, router, positionalRandomFactory, minBlockY, yBlockSize, globalFluidPicker);

    //CreateDisabled disabled aquifer factory, maps to vanilla createDisabled
    //Density > 0 returns null, otherwise returns globalFluidPicker's FluidStatus.At(blockY) at that coordinate
    static Aquifer CreateDisabled(FluidPicker fluidRule) => new DisabledAquifer(fluidRule);
}

//FluidStatus fluid status recording the fluid level and block state, maps to vanilla Aquifer.FluidStatus
//At(blockY) returns fluidType when blockY is below fluidLevel and AIR otherwise
public sealed class FluidStatus
{
    public int FluidLevel { get; }
    public BlockState FluidType { get; }

    public FluidStatus(int fluidLevel, BlockState fluidType)
    {
        FluidLevel = fluidLevel;
        FluidType = fluidType;
    }

    //At decides between fluidType and AIR from the coordinate y, maps to vanilla at
    public BlockState At(int blockY)
        => blockY < FluidLevel ? FluidType : Blocks.AIR.DefaultBlockState;

    public override bool Equals(object? obj)
        => obj is FluidStatus s && s.FluidLevel == FluidLevel && s.FluidType == FluidType;

    public override int GetHashCode() => HashCode.Combine(FluidLevel, FluidType);

    public static bool operator ==(FluidStatus a, FluidStatus b)
        => a.FluidLevel == b.FluidLevel && a.FluidType == b.FluidType;

    public static bool operator !=(FluidStatus a, FluidStatus b) => !(a == b);
}

//DisabledAquifer disabled aquifer, maps to the anonymous implementation in vanilla Aquifer.createDisabled
//Density > 0 returns null and NoiseBasedChunkGenerator falls back to Settings.DefaultBlock
//Density <= 0 returns globalFluidPicker's FluidStatus.At(blockY)
internal sealed class DisabledAquifer : Aquifer
{
    private readonly Aquifer.FluidPicker _fluidRule;

    public DisabledAquifer(Aquifer.FluidPicker fluidRule)
    {
        _fluidRule = fluidRule;
    }

    public BlockState? ComputeSubstance(FunctionContext context, double density)
    {
        if (density > 0.0) return null;
        return _fluidRule.ComputeFluid(context.BlockX, context.BlockY, context.BlockZ).At(context.BlockY);
    }

    public bool ShouldScheduleFluidUpdate() => false;
}

//NoiseBasedAquifer noise-based aquifer, maps to vanilla Aquifer.NoiseBasedAquifer
//The world is divided into 16x12x16 cells, each with a random centre point; the distance to the nearest four centres and pressure noise decide whether a cell is stone or fluid
public sealed class NoiseBasedAquifer : Aquifer
{
    //Source location cache unfilled marker, maps to vanilla DynamicGraphMinFixedPoint.SOURCE
    private const long Source = long.MaxValue;
    //WayBelowMinY sentinel below the minimum build height, maps to vanilla DimensionType.WAY_BELOW_MIN_Y
    private const int WayBelowMinY = -2032;
    //FlowingUpdateSimilarity similarity threshold that triggers a fluid update schedule, maps to the vanilla constant of the same name
    private static readonly double FlowingUpdateSimilarity = Similarity(Mth.Square(10), Mth.Square(12));
    //SurfaceSamplingOffsetsInChunks chunk offsets used when sampling the preliminary surface, maps to the vanilla constant of the same name
    private static readonly int[][] SurfaceSamplingOffsetsInChunks =
    {
        new[] { 0, 0 }, new[] { -2, -1 }, new[] { -1, -1 }, new[] { 0, -1 }, new[] { 1, -1 },
        new[] { -3, 0 }, new[] { -2, 0 }, new[] { -1, 0 }, new[] { 1, 0 },
        new[] { -2, 1 }, new[] { -1, 1 }, new[] { 0, 1 }, new[] { 1, 1 }
    };

    private readonly NoiseChunk _noiseChunk;
    private readonly DensityFunction _barrierNoise;
    private readonly DensityFunction _fluidLevelFloodednessNoise;
    private readonly DensityFunction _fluidLevelSpreadNoise;
    private readonly DensityFunction _lavaNoise;
    private readonly DensityFunction _erosion;
    private readonly DensityFunction _depth;
    private readonly PositionalRandomFactory _positionalRandomFactory;
    private readonly FluidStatus?[] _aquiferCache;
    private readonly long[] _aquiferLocationCache;
    private readonly Aquifer.FluidPicker _globalFluidPicker;
    private readonly int _skipSamplingAboveY;
    //_reusableContext per-cell reusable single-point context
    //One aquifer instance per chunk, driven by a single generation thread, so reuse never crosses threads
    //A chunk has nearly a hundred thousand cells; one At call per cell is the biggest small-object allocation in terrain generation
    private readonly SinglePointContext _reusableContext = new(0, 0, 0);
    private readonly int _minGridX;
    private readonly int _minGridY;
    private readonly int _minGridZ;
    private readonly int _gridSizeX;
    private readonly int _gridSizeZ;
    private bool _shouldScheduleFluidUpdate;

    public NoiseBasedAquifer(NoiseChunk noiseChunk, ChunkPos pos, NoiseRouter router,
        PositionalRandomFactory positionalRandomFactory, int minBlockY, int yBlockSize,
        Aquifer.FluidPicker globalFluidPicker)
    {
        _noiseChunk = noiseChunk;
        _barrierNoise = router.Barrier;
        _fluidLevelFloodednessNoise = router.FluidLevelFloodedness;
        _fluidLevelSpreadNoise = router.FluidLevelSpread;
        _lavaNoise = router.Lava;
        _erosion = router.Erosion;
        _depth = router.Depth;
        _positionalRandomFactory = positionalRandomFactory;
        _globalFluidPicker = globalFluidPicker;

        //Leave one extra grid cell on each side in X/Z and one in Y, so neighbour centres queried across chunks are still cached
        _minGridX = GridX(pos.MinBlockX - 5);
        var maxGridX = GridX(pos.MaxBlockX - 5) + 1;
        _gridSizeX = maxGridX - _minGridX + 1;
        _minGridY = GridY(minBlockY + 1) - 1;
        var maxGridY = GridY(minBlockY + yBlockSize + 1) + 1;
        var gridSizeY = maxGridY - _minGridY + 1;
        _minGridZ = GridZ(pos.MinBlockZ - 5);
        var maxGridZ = GridZ(pos.MaxBlockZ - 5) + 1;
        _gridSizeZ = maxGridZ - _minGridZ + 1;

        var totalGridSize = _gridSizeX * gridSizeY * _gridSizeZ;
        _aquiferCache = new FluidStatus?[totalGridSize];
        _aquiferLocationCache = new long[totalGridSize];
        Array.Fill(_aquiferLocationCache, Source);

        //Cells above this y get the global fluid directly without a centre search
        var maxAdjustedSurfaceLevel = AdjustSurfaceLevel(noiseChunk.MaxPreliminarySurfaceLevel(
            FromGridX(_minGridX, 0), FromGridZ(_minGridZ, 0), FromGridX(maxGridX, 9), FromGridZ(maxGridZ, 9)));
        var skipSamplingAboveGridY = GridY(maxAdjustedSurfaceLevel + 12) + 1;
        _skipSamplingAboveY = FromGridY(skipSamplingAboveGridY, 11) - 1;
    }

    //ComputeSubstance takes the fluid levels of the nearest four grid centres then applies the pressure test, maps to vanilla computeSubstance
    public BlockState? ComputeSubstance(FunctionContext context, double density)
    {
        if (density > 0.0)
        {
            _shouldScheduleFluidUpdate = false;
            return null;
        }
        var posX = context.BlockX;
        var posY = context.BlockY;
        var posZ = context.BlockZ;
        var globalFluid = _globalFluidPicker.ComputeFluid(posX, posY, posZ);
        if (posY > _skipSamplingAboveY)
        {
            _shouldScheduleFluidUpdate = false;
            return globalFluid.At(posY);
        }
        if (globalFluid.At(posY).Owner == Blocks.LAVA)
        {
            _shouldScheduleFluidUpdate = false;
            return Blocks.LAVA.DefaultBlockState;
        }

        //Pick the four nearest among the 2x3x2 grid centres
        var xAnchor = GridX(posX - 5);
        var yAnchor = GridY(posY + 1);
        var zAnchor = GridZ(posZ - 5);
        var distanceSqr1 = int.MaxValue;
        var distanceSqr2 = int.MaxValue;
        var distanceSqr3 = int.MaxValue;
        var distanceSqr4 = int.MaxValue;
        var closestIndex1 = 0;
        var closestIndex2 = 0;
        var closestIndex3 = 0;
        var closestIndex4 = 0;
        for (var x1 = 0; x1 <= 1; x1++)
        {
            for (var y1 = -1; y1 <= 1; y1++)
            {
                for (var z1 = 0; z1 <= 1; z1++)
                {
                    var spacedGridX = xAnchor + x1;
                    var spacedGridY = yAnchor + y1;
                    var spacedGridZ = zAnchor + z1;
                    var index = GetIndex(spacedGridX, spacedGridY, spacedGridZ);
                    var existingLocation = _aquiferLocationCache[index];
                    long location;
                    if (existingLocation != Source)
                    {
                        location = existingLocation;
                    }
                    else
                    {
                        var random = _positionalRandomFactory.At(spacedGridX, spacedGridY, spacedGridZ);
                        location = BlockPos.AsLong(
                            FromGridX(spacedGridX, random.NextInt(10)),
                            FromGridY(spacedGridY, random.NextInt(9)),
                            FromGridZ(spacedGridZ, random.NextInt(10)));
                        _aquiferLocationCache[index] = location;
                    }
                    var dx = BlockPos.GetX(location) - posX;
                    var dy = BlockPos.GetY(location) - posY;
                    var dz = BlockPos.GetZ(location) - posZ;
                    var newDistance = dx * dx + dy * dy + dz * dz;
                    if (distanceSqr1 >= newDistance)
                    {
                        closestIndex4 = closestIndex3;
                        closestIndex3 = closestIndex2;
                        closestIndex2 = closestIndex1;
                        closestIndex1 = index;
                        distanceSqr4 = distanceSqr3;
                        distanceSqr3 = distanceSqr2;
                        distanceSqr2 = distanceSqr1;
                        distanceSqr1 = newDistance;
                    }
                    else if (distanceSqr2 >= newDistance)
                    {
                        closestIndex4 = closestIndex3;
                        closestIndex3 = closestIndex2;
                        closestIndex2 = index;
                        distanceSqr4 = distanceSqr3;
                        distanceSqr3 = distanceSqr2;
                        distanceSqr2 = newDistance;
                    }
                    else if (distanceSqr3 >= newDistance)
                    {
                        closestIndex4 = closestIndex3;
                        closestIndex3 = index;
                        distanceSqr4 = distanceSqr3;
                        distanceSqr3 = newDistance;
                    }
                    else if (distanceSqr4 >= newDistance)
                    {
                        closestIndex4 = index;
                        distanceSqr4 = newDistance;
                    }
                }
            }
        }

        var closestStatus1 = GetAquiferStatus(closestIndex1);
        var similarity12 = Similarity(distanceSqr1, distanceSqr2);
        var fluidState = closestStatus1.At(posY);
        if (similarity12 <= 0.0)
        {
            _shouldScheduleFluidUpdate = similarity12 >= FlowingUpdateSimilarity
                && !closestStatus1.Equals(GetAquiferStatus(closestIndex2));
            return fluidState;
        }
        //When water sits on top of lava, force one fluid update so the water surface falls
        if (fluidState.Owner == Blocks.WATER
            && _globalFluidPicker.ComputeFluid(posX, posY - 1, posZ).At(posY - 1).Owner == Blocks.LAVA)
        {
            _shouldScheduleFluidUpdate = true;
            return fluidState;
        }

        var barrierNoiseValue = double.NaN;
        var closestStatus2 = GetAquiferStatus(closestIndex2);
        var barrier12 = similarity12 * CalculatePressure(context, ref barrierNoiseValue, closestStatus1, closestStatus2);
        if (density + barrier12 > 0.0)
        {
            _shouldScheduleFluidUpdate = false;
            return null;
        }
        var closestStatus3 = GetAquiferStatus(closestIndex3);
        var similarity13 = Similarity(distanceSqr1, distanceSqr3);
        if (similarity13 > 0.0)
        {
            var barrier13 = similarity12 * similarity13
                * CalculatePressure(context, ref barrierNoiseValue, closestStatus1, closestStatus3);
            if (density + barrier13 > 0.0)
            {
                _shouldScheduleFluidUpdate = false;
                return null;
            }
        }
        var similarity23 = Similarity(distanceSqr2, distanceSqr3);
        if (similarity23 > 0.0)
        {
            var barrier23 = similarity12 * similarity23
                * CalculatePressure(context, ref barrierNoiseValue, closestStatus2, closestStatus3);
            if (density + barrier23 > 0.0)
            {
                _shouldScheduleFluidUpdate = false;
                return null;
            }
        }

        var mayFlow12 = !closestStatus1.Equals(closestStatus2);
        var mayFlow23 = similarity23 >= FlowingUpdateSimilarity && !closestStatus2.Equals(closestStatus3);
        var mayFlow13 = similarity13 >= FlowingUpdateSimilarity && !closestStatus1.Equals(closestStatus3);
        if (mayFlow12 || mayFlow23 || mayFlow13)
            _shouldScheduleFluidUpdate = true;
        else
            _shouldScheduleFluidUpdate = similarity13 >= FlowingUpdateSimilarity
                && Similarity(distanceSqr1, distanceSqr4) >= FlowingUpdateSimilarity
                && !closestStatus1.Equals(GetAquiferStatus(closestIndex4));
        return fluidState;
    }

    public bool ShouldScheduleFluidUpdate() => _shouldScheduleFluidUpdate;

    //Similarity grows as two squared distances get closer, maps to vanilla similarity
    private static double Similarity(int distanceSqr1, int distanceSqr2)
        => 1.0 - (distanceSqr2 - distanceSqr1) / 25.0;

    //CalculatePressure pressure from the fluid-level difference of two centres; barrierNoiseValue caches the barrier noise for the same coordinate
    private double CalculatePressure(FunctionContext context, ref double barrierNoiseValue,
        FluidStatus statusClosest1, FluidStatus statusClosest2)
    {
        var posY = context.BlockY;
        var type1 = statusClosest1.At(posY);
        var type2 = statusClosest2.At(posY);
        if ((type1.Owner == Blocks.LAVA && type2.Owner == Blocks.WATER)
            || (type1.Owner == Blocks.WATER && type2.Owner == Blocks.LAVA))
            return 2.0;

        var fluidYDiff = Math.Abs(statusClosest1.FluidLevel - statusClosest2.FluidLevel);
        if (fluidYDiff == 0) return 0.0;
        var averageFluidY = 0.5 * (statusClosest1.FluidLevel + statusClosest2.FluidLevel);
        var howFarAboveAverageFluidPoint = posY + 0.5 - averageFluidY;
        var baseValue = fluidYDiff / 2.0;
        var distanceFromBarrierEdgeTowardsMiddle = baseValue - Math.Abs(howFarAboveAverageFluidPoint);
        double gradient;
        if (howFarAboveAverageFluidPoint > 0.0)
        {
            var centerPoint = 0.0 + distanceFromBarrierEdgeTowardsMiddle;
            gradient = centerPoint > 0.0 ? centerPoint / 1.5 : centerPoint / 2.5;
        }
        else
        {
            var centerPoint = 3.0 + distanceFromBarrierEdgeTowardsMiddle;
            gradient = centerPoint > 0.0 ? centerPoint / 3.0 : centerPoint / 10.0;
        }

        double noiseValue;
        if (gradient < -2.0 || gradient > 2.0)
        {
            noiseValue = 0.0;
        }
        else if (double.IsNaN(barrierNoiseValue))
        {
            barrierNoiseValue = _barrierNoise.Compute(context);
            noiseValue = barrierNoiseValue;
        }
        else
        {
            noiseValue = barrierNoiseValue;
        }
        return 2.0 * (noiseValue + gradient);
    }

    //GridX block coordinate to X grid coordinate, maps to vanilla gridX
    private static int GridX(int blockCoord) => blockCoord >> 4;

    //FromGridX grid coordinate back to block coordinate, maps to vanilla fromGridX
    private static int FromGridX(int gridCoord, int blockOffset) => (gridCoord << 4) + blockOffset;

    //GridY block coordinate to Y grid coordinate, maps to vanilla gridY
    private static int GridY(int blockCoord) => Mth.FloorDiv(blockCoord, 12);

    //FromGridY grid coordinate back to block coordinate, maps to vanilla fromGridY
    private static int FromGridY(int gridCoord, int blockOffset) => gridCoord * 12 + blockOffset;

    //GridZ block coordinate to Z grid coordinate, maps to vanilla gridZ
    private static int GridZ(int blockCoord) => blockCoord >> 4;

    //FromGridZ grid coordinate back to block coordinate, maps to vanilla fromGridZ
    private static int FromGridZ(int gridCoord, int blockOffset) => (gridCoord << 4) + blockOffset;

    //GetIndex 3D grid coordinate to a 1D index
    private int GetIndex(int gridX, int gridY, int gridZ)
        => ((gridY - _minGridY) * _gridSizeZ + (gridZ - _minGridZ)) * _gridSizeX + (gridX - _minGridX);

    //GetAquiferStatus lazily computes and caches the fluid status of a grid centre, maps to vanilla getAquiferStatus
    private FluidStatus GetAquiferStatus(int index)
    {
        var oldStatus = _aquiferCache[index];
        if (oldStatus is not null) return oldStatus;
        var location = _aquiferLocationCache[index];
        var status = ComputeFluid(BlockPos.GetX(location), BlockPos.GetY(location), BlockPos.GetZ(location));
        _aquiferCache[index] = status;
        return status;
    }

    //ComputeFluid decides the fluid level and type of a cell from the surface heights of the 13 chunks around the centre, maps to vanilla computeFluid
    private FluidStatus ComputeFluid(int x, int y, int z)
    {
        var globalFluid = _globalFluidPicker.ComputeFluid(x, y, z);
        var lowestPreliminarySurface = int.MaxValue;
        var topOfAquiferCell = y + 12;
        var bottomOfAquiferCell = y - 12;
        var surfaceAtCenterIsUnderGlobalFluidLevel = false;
        foreach (var offset in SurfaceSamplingOffsetsInChunks)
        {
            var sampleX = x + (offset[0] << 4);
            var sampleZ = z + (offset[1] << 4);
            var preliminarySurfaceLevel = _noiseChunk.PreliminarySurfaceLevel(sampleX, sampleZ);
            var adjustedSurfaceLevel = AdjustSurfaceLevel(preliminarySurfaceLevel);
            var start = offset[0] == 0 && offset[1] == 0;
            if (start && bottomOfAquiferCell > adjustedSurfaceLevel) return globalFluid;
            var topOfAquiferCellPokesAboveSurface = topOfAquiferCell > adjustedSurfaceLevel;
            if (topOfAquiferCellPokesAboveSurface || start)
            {
                var globalFluidAtSurface = _globalFluidPicker.ComputeFluid(sampleX, adjustedSurfaceLevel, sampleZ);
                if (globalFluidAtSurface.At(adjustedSurfaceLevel).Owner is BlockBehaviour { IsAir: true })
                    continue;
                if (start) surfaceAtCenterIsUnderGlobalFluidLevel = true;
                if (topOfAquiferCellPokesAboveSurface) return globalFluidAtSurface;
            }
            lowestPreliminarySurface = Math.Min(lowestPreliminarySurface, preliminarySurfaceLevel);
        }
        var fluidSurfaceLevel = ComputeSurfaceLevel(x, y, z, globalFluid, lowestPreliminarySurface,
            surfaceAtCenterIsUnderGlobalFluidLevel);
        return new FluidStatus(fluidSurfaceLevel, ComputeFluidType(x, y, z, globalFluid, fluidSurfaceLevel));
    }

    //AdjustSurfaceLevel adds 8 to the preliminary surface as the aquifer reference height, maps to vanilla adjustSurfaceLevel
    private static int AdjustSurfaceLevel(int preliminarySurfaceLevel) => preliminarySurfaceLevel + 8;

    //ComputeSurfaceLevel: floodedness noise decides the fluid level; fully flooded takes the global level, partially flooded takes a random level, otherwise no fluid
    private int ComputeSurfaceLevel(int x, int y, int z, FluidStatus globalFluid, int lowestPreliminarySurface,
        bool surfaceAtCenterIsUnderGlobalFluidLevel)
    {
        var context = _reusableContext.Set(x, y, z);
        double partiallyFloodedness;
        double fullyFloodedness;
        if (OverworldBiomeBuilder.IsDeepDarkRegion(_erosion, _depth, context))
        {
            partiallyFloodedness = -1.0;
            fullyFloodedness = -1.0;
        }
        else
        {
            var distanceBelowSurface = lowestPreliminarySurface + 8 - y;
            var floodednessFactor = surfaceAtCenterIsUnderGlobalFluidLevel
                ? Mth.ClampedMap(distanceBelowSurface, 0.0, 64.0, 1.0, 0.0)
                : 0.0;
            var floodednessNoiseValue = Mth.Clamp(_fluidLevelFloodednessNoise.Compute(context), -1.0, 1.0);
            fullyFloodedness = floodednessNoiseValue - Mth.Map(floodednessFactor, 1.0, 0.0, -0.3, 0.8);
            partiallyFloodedness = floodednessNoiseValue - Mth.Map(floodednessFactor, 1.0, 0.0, -0.8, 0.4);
        }
        if (fullyFloodedness > 0.0) return globalFluid.FluidLevel;
        if (partiallyFloodedness > 0.0) return ComputeRandomizedFluidSurfaceLevel(x, y, z, lowestPreliminarySurface);
        return WayBelowMinY;
    }

    //ComputeRandomizedFluidSurfaceLevel randomises the fluid level from spread noise over 64x40x16 cells and clamps it below the lowest surface
    private int ComputeRandomizedFluidSurfaceLevel(int x, int y, int z, int lowestPreliminarySurface)
    {
        var fluidLevelCellX = Mth.FloorDiv(x, 16);
        var fluidLevelCellY = Mth.FloorDiv(y, 40);
        var fluidLevelCellZ = Mth.FloorDiv(z, 16);
        var fluidCellMiddleY = fluidLevelCellY * 40 + 20;
        var fluidLevelSpread = _fluidLevelSpreadNoise.Compute(
            _reusableContext.Set(fluidLevelCellX, fluidLevelCellY, fluidLevelCellZ)) * 10.0;
        var targetFluidSurfaceLevel = fluidCellMiddleY + Mth.Quantize(fluidLevelSpread, 3);
        return Math.Min(lowestPreliminarySurface, targetFluidSurfaceLevel);
    }

    //ComputeFluidType swaps water for lava when the fluid level is low enough and the lava noise is large enough, maps to vanilla computeFluidType
    private BlockState ComputeFluidType(int x, int y, int z, FluidStatus globalFluid, int fluidSurfaceLevel)
    {
        var fluidType = globalFluid.FluidType;
        if (fluidSurfaceLevel <= -10 && fluidSurfaceLevel != WayBelowMinY
            && globalFluid.FluidType.Owner != Blocks.LAVA)
        {
            var fluidTypeCellX = Mth.FloorDiv(x, 64);
            var fluidTypeCellY = Mth.FloorDiv(y, 40);
            var fluidTypeCellZ = Mth.FloorDiv(z, 64);
            var lavaNoiseValue = _lavaNoise.Compute(
                _reusableContext.Set(fluidTypeCellX, fluidTypeCellY, fluidTypeCellZ));
            if (Math.Abs(lavaNoiseValue) > 0.3) fluidType = Blocks.LAVA.DefaultBlockState;
        }
        return fluidType;
    }
}
