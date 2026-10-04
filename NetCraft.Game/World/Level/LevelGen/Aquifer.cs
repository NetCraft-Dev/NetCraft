using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Util;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen;

//Aquifer 含水层接口对应原版 net.minecraft.world.level.levelgen.Aquifer
//根据 NoiseRouter 的 Barrier/FluidLevelFloodedness/FluidLevelSpread/Lava 与 globalFluidPicker 决定每格方块状态
//两种实现 NoiseBasedAquifer 完整含水层 DisabledAquifer 简化版密度>0 返回 null 否则返回全局流体
public interface Aquifer
{
    //FluidPicker 流体选择器按坐标返回 FluidStatus 对应原版 Aquifer.FluidPicker
    public interface FluidPicker
    {
        FluidStatus ComputeFluid(int blockX, int blockY, int blockZ);
    }

    //ComputeSubstance 按上下文与密度值返回方块状态密度>0 通常返回 null 由上层用 DefaultBlock 兜底
    BlockState? ComputeSubstance(FunctionContext context, double density);

    //ShouldScheduleFluidUpdate 是否需要调度流体更新对应原版 shouldScheduleFluidUpdate
    bool ShouldScheduleFluidUpdate();

    //Create 完整含水层工厂对应原版 create
    //P0 阶段 NoiseBasedAquifer 内部委托 DisabledAquifer 留好构造签名便于 P1 补全含水层网格算法
    static Aquifer Create(NoiseChunk noiseChunk, ChunkPos pos, NoiseRouter router,
        PositionalRandomFactory positionalRandomFactory, int minBlockY, int yBlockSize,
        FluidPicker globalFluidPicker)
        => new NoiseBasedAquifer(noiseChunk, pos, router, positionalRandomFactory, minBlockY, yBlockSize, globalFluidPicker);

    //CreateDisabled 禁用含水层工厂对应原版 createDisabled
    //密度>0 返回 null 否则返回 globalFluidPicker 在该坐标的 FluidStatus.At(blockY)
    static Aquifer CreateDisabled(FluidPicker fluidRule) => new DisabledAquifer(fluidRule);
}

//FluidStatus 流体状态记录流体液面高度与方块状态对应原版 Aquifer.FluidStatus
//At(blockY) 当 blockY 小于 fluidLevel 返回 fluidType 否则返回 AIR
public sealed class FluidStatus
{
    public int FluidLevel { get; }
    public BlockState FluidType { get; }

    public FluidStatus(int fluidLevel, BlockState fluidType)
    {
        FluidLevel = fluidLevel;
        FluidType = fluidType;
    }

    //At 按坐标 y 决定返回 fluidType 还是 AIR 对应原版 at
    public BlockState At(int blockY)
        => blockY < FluidLevel ? FluidType : Blocks.AIR.DefaultBlockState;

    public override bool Equals(object? obj)
        => obj is FluidStatus s && s.FluidLevel == FluidLevel && s.FluidType == FluidType;

    public override int GetHashCode() => HashCode.Combine(FluidLevel, FluidType);

    public static bool operator ==(FluidStatus a, FluidStatus b)
        => a.FluidLevel == b.FluidLevel && a.FluidType == b.FluidType;

    public static bool operator !=(FluidStatus a, FluidStatus b) => !(a == b);
}

//DisabledAquifer 禁用含水层对应原版 Aquifer.createDisabled 匿名实现
//密度>0 返回 null 由 NoiseBasedChunkGenerator 用 Settings.DefaultBlock 兜底
//密度<=0 返回 globalFluidPicker 的 FluidStatus.At(blockY)
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

//NoiseBasedAquifer 噪声含水层对应原版 Aquifer.NoiseBasedAquifer
//世界按 16x12x16 切网格每格随机一个中心点按到最近四个中心的距离与水压噪声决定该格是石头还是流体
public sealed class NoiseBasedAquifer : Aquifer
{
    //Source 位置缓存未填充标记对应原版 DynamicGraphMinFixedPoint.SOURCE
    private const long Source = long.MaxValue;
    //WayBelowMinY 低于最低建筑高度的哨兵对应原版 DimensionType.WAY_BELOW_MIN_Y
    private const int WayBelowMinY = -2032;
    //FlowingUpdateSimilarity 触发流体更新调度的相似度阈值对应原版同名常量
    private static readonly double FlowingUpdateSimilarity = Similarity(Mth.Square(10), Mth.Square(12));
    //SurfaceSamplingOffsetsInChunks 采初步地表时的区块偏移对应原版同名常量
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
    //_reusableContext 逐格采样复用的单点上下文
    //含水层实例每区块一个且由单个生成线程驱动 复用不会跨线程
    //一个区块近十万格 逐格 At 一次是地形生成里最大的一处小对象分配
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

        //X/Z 网格向外各留一格 Y 也留一格保证跨区块查询邻居中心也在缓存内
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

        //高于该 y 的网格直接给全局流体不再做中心搜索
        var maxAdjustedSurfaceLevel = AdjustSurfaceLevel(noiseChunk.MaxPreliminarySurfaceLevel(
            FromGridX(_minGridX, 0), FromGridZ(_minGridZ, 0), FromGridX(maxGridX, 9), FromGridZ(maxGridZ, 9)));
        var skipSamplingAboveGridY = GridY(maxAdjustedSurfaceLevel + 12) + 1;
        _skipSamplingAboveY = FromGridY(skipSamplingAboveGridY, 11) - 1;
    }

    //ComputeSubstance 取最近四个网格中心的液面再做水压判定对应原版 computeSubstance
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

        //在 2x3x2 个网格中心里挑距离最近的前四个
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
        //水盖在岩浆上时强制调度一次流体更新让水面回落
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

    //Similarity 两个距离平方越接近值越大对应原版 similarity
    private static double Similarity(int distanceSqr1, int distanceSqr2)
        => 1.0 - (distanceSqr2 - distanceSqr1) / 25.0;

    //CalculatePressure 两个中心液面差产生的压力用 barrierNoiseValue 缓存同一坐标的屏障噪声
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

    //GridX 方块坐标到 X 网格坐标对应原版 gridX
    private static int GridX(int blockCoord) => blockCoord >> 4;

    //FromGridX 网格坐标还原方块坐标对应原版 fromGridX
    private static int FromGridX(int gridCoord, int blockOffset) => (gridCoord << 4) + blockOffset;

    //GridY 方块坐标到 Y 网格坐标对应原版 gridY
    private static int GridY(int blockCoord) => Mth.FloorDiv(blockCoord, 12);

    //FromGridY 网格坐标还原方块坐标对应原版 fromGridY
    private static int FromGridY(int gridCoord, int blockOffset) => gridCoord * 12 + blockOffset;

    //GridZ 方块坐标到 Z 网格坐标对应原版 gridZ
    private static int GridZ(int blockCoord) => blockCoord >> 4;

    //FromGridZ 网格坐标还原方块坐标对应原版 fromGridZ
    private static int FromGridZ(int gridCoord, int blockOffset) => (gridCoord << 4) + blockOffset;

    //GetIndex 三维网格坐标到一维下标
    private int GetIndex(int gridX, int gridY, int gridZ)
        => ((gridY - _minGridY) * _gridSizeZ + (gridZ - _minGridZ)) * _gridSizeX + (gridX - _minGridX);

    //GetAquiferStatus 懒算网格中心的流体状态并缓存对应原版 getAquiferStatus
    private FluidStatus GetAquiferStatus(int index)
    {
        var oldStatus = _aquiferCache[index];
        if (oldStatus is not null) return oldStatus;
        var location = _aquiferLocationCache[index];
        var status = ComputeFluid(BlockPos.GetX(location), BlockPos.GetY(location), BlockPos.GetZ(location));
        _aquiferCache[index] = status;
        return status;
    }

    //ComputeFluid 按中心点周围 13 个区块的地表高度决定该格液面与流体类型对应原版 computeFluid
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

    //AdjustSurfaceLevel 初步地表加 8 作为含水层参考高度对应原版 adjustSurfaceLevel
    private static int AdjustSurfaceLevel(int preliminarySurfaceLevel) => preliminarySurfaceLevel + 8;

    //ComputeSurfaceLevel 由淹没度噪声决定液面完全淹没取全局液面部分淹没取随机液面否则无流体
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

    //ComputeRandomizedFluidSurfaceLevel 按 64x40x16 格上的展布噪声随机液面并压到最低地表以下
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

    //ComputeFluidType 液面够低且岩浆噪声够大时把水换成岩浆对应原版 computeFluidType
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
