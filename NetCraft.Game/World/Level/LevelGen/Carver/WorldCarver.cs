using NetCraft.Game.World.Level.Block;
using NetCraft.Game.World.Level.LevelGen;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Util;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Carver;

//WorldCarver abstract carver base, maps to vanilla net.minecraft.world.level.levelgen.carver.WorldCarver
//Generic parameter from vanilla is dropped; subclasses pattern-match to their config type in Carve/IsStartChunk
//CarveEllipsoid sweeps voxels in an ellipsoid; CarveBlock decides whether each cell can be replaced and with what
public abstract class WorldCarver : NetCraft.Registry.WorldCarver
{
    //Air air state, the default result after carving out a cell
    protected static readonly BlockState Air = Blocks.AIR.DefaultBlockState;

    //CaveAir cave air state, cached as a static field to avoid hitting the registry every time
    protected static readonly BlockState CaveAir = Blocks.CaveAir.DefaultBlockState;

    //CarveSkipChecker voxel skip test, maps to vanilla WorldCarver.CarveSkipChecker
    protected delegate bool CarveSkipChecker(CarvingContext context, double xd, double yd, double zd, int y);

    //CAVE cave carver; registered into the CARVER registry together with NETHER_CAVE/CANYON during bootstrap
    public static readonly CaveWorldCarver CAVE = new(Identifier.WithDefaultNamespace("cave"));
    public static readonly NetherWorldCarver NETHER_CAVE = new(Identifier.WithDefaultNamespace("nether_cave"));
    public static readonly CanyonWorldCarver CANYON = new(Identifier.WithDefaultNamespace("canyon"));

    public Identifier Id { get; }

    protected WorldCarver(Identifier id) => Id = id;

    //Range carving radius in chunks, maps to vanilla getRange
    public virtual int Range => 4;

    //RegisterAll register the three built-in carvers into the CARVER registry
    public static void RegisterAll()
    {
        Registry<NetCraft.Registry.WorldCarver>.Register(BuiltInRegistries.CARVER, CAVE.Id, CAVE);
        Registry<NetCraft.Registry.WorldCarver>.Register(BuiltInRegistries.CARVER, NETHER_CAVE.Id, NETHER_CAVE);
        Registry<NetCraft.Registry.WorldCarver>.Register(BuiltInRegistries.CARVER, CANYON.Id, CANYON);
    }

    //IsStartChunk whether this chunk should start a carve, maps to vanilla isStartChunk
    public abstract bool IsStartChunk(CarverConfiguration config, RandomSource random);

    //Carve run one carve over the chunk, maps to vanilla carve; start point and seed are supplied by the caller
    public abstract bool Carve(CarvingContext context, CarverConfiguration config, ChunkAccess chunk,
        Func<int, int, int, Biome> biomeGetter, RandomSource random, Aquifer aquifer, ChunkPos sourceChunkPos,
        CarvingMask mask);

    //CarveEllipsoid sweep voxels inside an ellipsoid, maps to vanilla carveEllipsoid
    protected bool CarveEllipsoid(CarvingContext context, CarverConfiguration config, ChunkAccess chunk,
        Func<int, int, int, Biome> biomeGetter, Aquifer aquifer, double x, double y, double z,
        double horizontalRadius, double verticalRadius, CarvingMask mask, CarveSkipChecker skipChecker)
    {
        var chunkPos = chunk.Pos;
        var centerX = chunkPos.MinBlockX + 8.0;
        var centerZ = chunkPos.MinBlockZ + 8.0;
        var maxDelta = 16.0 + horizontalRadius * 2.0;
        if (Math.Abs(x - centerX) > maxDelta || Math.Abs(z - centerZ) > maxDelta) return false;
        var chunkMinX = chunkPos.MinBlockX;
        var chunkMinZ = chunkPos.MinBlockZ;
        var minXIndex = Math.Max((Mth.Floor(x - horizontalRadius) - chunkMinX) - 1, 0);
        var maxXIndex = Math.Min(Mth.Floor(x + horizontalRadius) - chunkMinX, 15);
        var minY = Math.Max(Mth.Floor(y - verticalRadius) - 1, context.GetMinGenY() + 1);
        //Leave seven blocks at the top as a protective layer so bedrock and the surface are not carved through
        var maxY = Math.Min(Mth.Floor(y + verticalRadius) + 1,
            (context.GetMinGenY() + context.GetGenDepth() - 1) - 7);
        var minZIndex = Math.Max((Mth.Floor(z - horizontalRadius) - chunkMinZ) - 1, 0);
        var maxZIndex = Math.Min(Mth.Floor(z + horizontalRadius) - chunkMinZ, 15);
        var carved = false;
        for (var xIndex = minXIndex; xIndex <= maxXIndex; xIndex++)
        {
            var worldX = chunkMinX + xIndex;
            var xd = ((worldX + 0.5) - x) / horizontalRadius;
            for (var zIndex = minZIndex; zIndex <= maxZIndex; zIndex++)
            {
                var worldZ = chunkMinZ + zIndex;
                var zd = ((worldZ + 0.5) - z) / horizontalRadius;
                if (xd * xd + zd * zd >= 1.0) continue;
                var hasGrass = false;
                for (var worldY = maxY; worldY > minY; worldY--)
                {
                    var yd = ((worldY - 0.5) - y) / verticalRadius;
                    if (skipChecker(context, xd, yd, zd, worldY)) continue;
                    if (mask.Get(xIndex, worldY, zIndex) && !IsDebugEnabled(config)) continue;
                    mask.Set(xIndex, worldY, zIndex);
                    carved |= CarveBlock(context, config, chunk, biomeGetter, worldX, worldY, worldZ, aquifer,
                        ref hasGrass);
                }
            }
        }
        return carved;
    }

    //CarveBlock decide whether a single cell is replaceable and write the carve result, maps to vanilla carveBlock
    protected virtual bool CarveBlock(CarvingContext context, CarverConfiguration config, ChunkAccess chunk,
        Func<int, int, int, Biome> biomeGetter, int x, int y, int z, Aquifer aquifer, ref bool hasGrass)
    {
        var blockState = chunk.GetBlockState(x, y, z);
        if (blockState.Owner == Blocks.GRASS_BLOCK || blockState.Owner == Blocks.Mycelium) hasGrass = true;
        if (!CanReplaceBlock(config, blockState) && !IsDebugEnabled(config)) return false;
        var state = GetCarveState(context, config, x, y, z, aquifer);
        if (state is null) return false;
        chunk.SetBlockState(x, y, z, state.Value);
        if (aquifer.ShouldScheduleFluidUpdate() && !state.Value.FluidState.IsEmpty)
            chunk.MarkPosForPostProcessing(x, y, z);
        if (!hasGrass) return true;
        //After carving through grass, the cell below it needs its top material recomputed by the surface rules
        var belowY = y - 1;
        if (chunk.GetBlockState(x, belowY, z).Owner != Blocks.DIRT) return true;
        var topMaterial = context.TopMaterial(biomeGetter, chunk, x, belowY, z, !state.Value.FluidState.IsEmpty);
        if (topMaterial is null) return true;
        chunk.SetBlockState(x, belowY, z, topMaterial.Value);
        if (!topMaterial.Value.FluidState.IsEmpty) chunk.MarkPosForPostProcessing(x, belowY, z);
        return true;
    }

    //GetCarveState what goes into this cell; lava below the lava level, otherwise ask the aquifer. maps to vanilla getCarveState
    private BlockState? GetCarveState(CarvingContext context, CarverConfiguration config, int x, int y, int z,
        Aquifer aquifer)
    {
        if (y <= config.LavaLevel.ResolveY(context)) return Blocks.LavaFluidState.CreateLegacyBlock();
        var state = aquifer.ComputeSubstance(SinglePointContext.At(x, y, z), 0.0);
        if (state is not null) return IsDebugEnabled(config) ? GetDebugState(config, state.Value) : state;
        return IsDebugEnabled(config) ? config.DebugSettings.BarrierState : null;
    }

    //GetDebugState in debug mode swap air/water/lava for eye-catching debug blocks, maps to vanilla getDebugState
    private static BlockState GetDebugState(CarverConfiguration config, BlockState state)
    {
        if (state.Owner == Blocks.AIR) return config.DebugSettings.AirState;
        if (state.Owner == Blocks.WATER) return config.DebugSettings.WaterState;
        if (state.Owner == Blocks.LAVA) return config.DebugSettings.LavaState;
        return state;
    }

    //CanReplaceBlock whether the block is in the configured replaceable set, maps to vanilla canReplaceBlock
    //While the tag is unbound (data not loaded) treat it as not replaceable, so Holder.Is does not throw on an unbound tag
    protected static bool CanReplaceBlock(CarverConfiguration config, BlockState state)
    {
        if (config.Replaceable is NamedHolderSet<NetCraft.Registry.Block> named && !named.IsBound) return false;
        return config.Replaceable.Contains(BuiltInRegistries.BLOCK.WrapAsHolder(state.Owner));
    }

    //CanReach whether the tunnel step is still within this chunk's influence range, maps to vanilla canReach
    protected static bool CanReach(ChunkPos chunkPos, double x, double z, int currentStep, int totalSteps,
        float thickness)
    {
        var xd = x - (chunkPos.MinBlockX + 8.0);
        var zd = z - (chunkPos.MinBlockZ + 8.0);
        var remaining = totalSteps - currentStep;
        var rr = thickness + 2.0f + 16.0f;
        return xd * xd + zd * zd - remaining * remaining <= rr * rr;
    }

    private static bool IsDebugEnabled(CarverConfiguration config) => config.DebugSettings.DebugMode;
}

//CaveWorldCarver cave carver, maps to vanilla CaveWorldCarver
//Scatter a few cave origins at random; each may first carve a room then branch into several tunnels
public class CaveWorldCarver : WorldCarver
{
    public CaveWorldCarver(Identifier id) : base(id) { }

    public override bool IsStartChunk(CarverConfiguration config, RandomSource random)
        => random.NextFloat() <= config.Probability;

    public override bool Carve(CarvingContext context, CarverConfiguration config, ChunkAccess chunk,
        Func<int, int, int, Biome> biomeGetter, RandomSource random, Aquifer aquifer, ChunkPos sourceChunkPos,
        CarvingMask mask)
    {
        if (config is not CaveCarverConfiguration caveConfig)
            throw new ArgumentException($"cave carver requires {nameof(CaveCarverConfiguration)}: {config.GetType().Name}");
        var maxDistance = (Range * 2 - 1) * 16;
        var caveCount = random.NextInt(random.NextInt(random.NextInt(GetCaveBound()) + 1) + 1);
        for (var cave = 0; cave < caveCount; cave++)
        {
            var x = (double)(sourceChunkPos.MinBlockX + random.NextInt(16));
            double y = caveConfig.Y.Sample(random, context);
            var z = (double)(sourceChunkPos.MinBlockZ + random.NextInt(16));
            var horizontalRadiusMultiplier = caveConfig.HorizontalRadiusMultiplier.Sample(random);
            var verticalRadiusMultiplier = caveConfig.VerticalRadiusMultiplier.Sample(random);
            var floorLevel = caveConfig.FloorLevel.Sample(random);
            CarveSkipChecker skipChecker = (_, xd, yd, zd, _) => ShouldSkip(xd, yd, zd, floorLevel);
            var tunnels = 1;
            if (random.NextInt(4) == 0)
            {
                var yScale = caveConfig.YScale.Sample(random);
                var thickness = 1.0f + random.NextFloat() * 6.0f;
                CreateRoom(context, caveConfig, chunk, biomeGetter, aquifer, x, y, z, thickness, yScale, mask,
                    skipChecker);
                tunnels = 1 + random.NextInt(4);
            }
            for (var i = 0; i < tunnels; i++)
            {
                var horizontalRotation = random.NextFloat() * 6.2831855f;
                var verticalRotation = (random.NextFloat() - 0.5f) / 4.0f;
                var thickness = GetThickness(random);
                var distance = maxDistance - random.NextInt(maxDistance / 4);
                CreateTunnel(context, caveConfig, chunk, biomeGetter, random.NextLong(), aquifer, x, y, z,
                    horizontalRadiusMultiplier, verticalRadiusMultiplier, thickness, horizontalRotation,
                    verticalRotation, 0, distance, GetYScale(), mask, skipChecker);
            }
        }
        return true;
    }

    //GetCaveBound max caves per chunk, maps to vanilla getCaveBound
    protected virtual int GetCaveBound() => 15;

    //GetThickness tunnel thickness, maps to vanilla getThickness
    protected virtual float GetThickness(RandomSource random)
    {
        var thickness = random.NextFloat() * 2.0f + random.NextFloat();
        if (random.NextInt(10) == 0) thickness *= random.NextFloat() * random.NextFloat() * 3.0f + 1.0f;
        return thickness;
    }

    protected virtual double GetYScale() => 1.0;

    //CreateRoom carve a spherical room at the cave origin, maps to vanilla createRoom
    protected virtual void CreateRoom(CarvingContext context, CaveCarverConfiguration config, ChunkAccess chunk,
        Func<int, int, int, Biome> biomeGetter, Aquifer aquifer, double x, double y, double z, float thickness,
        double yScale, CarvingMask mask, CarveSkipChecker skipChecker)
    {
        var horizontalRadius = 1.5 + Mth.Sin(1.5707963705062866) * thickness;
        var verticalRadius = horizontalRadius * yScale;
        CarveEllipsoid(context, config, chunk, biomeGetter, aquifer, x + 1.0, y, z, horizontalRadius, verticalRadius,
            mask, skipChecker);
    }

    //CreateTunnel advance along random turns, carving an ellipsoid per step, maps to vanilla createTunnel
    protected virtual void CreateTunnel(CarvingContext context, CaveCarverConfiguration config, ChunkAccess chunk,
        Func<int, int, int, Biome> biomeGetter, long tunnelSeed, Aquifer aquifer, double x, double y, double z,
        double horizontalRadiusMultiplier, double verticalRadiusMultiplier, float thickness,
        float horizontalRotation, float verticalRotation, int step, int dist, double yScale, CarvingMask mask,
        CarveSkipChecker skipChecker)
    {
        var random = RandomSource.Create(tunnelSeed);
        var splitPoint = random.NextInt(dist / 2) + dist / 4;
        var steep = random.NextInt(6) == 0;
        var yRota = 0.0f;
        var xRota = 0.0f;
        for (var currentStep = step; currentStep < dist; currentStep++)
        {
            var horizontalRadius = 1.5 + Mth.Sin(3.1415927f * currentStep / dist) * thickness;
            var verticalRadius = horizontalRadius * yScale;
            var cosX = Mth.Cos(verticalRotation);
            x += Mth.Cos(horizontalRotation) * cosX;
            y += Mth.Sin(verticalRotation);
            z += Mth.Sin(horizontalRotation) * cosX;
            verticalRotation = verticalRotation * (steep ? 0.92f : 0.7f) + xRota * 0.1f;
            horizontalRotation += yRota * 0.1f;
            xRota = xRota * 0.9f + (random.NextFloat() - random.NextFloat()) * random.NextFloat() * 2.0f;
            yRota = yRota * 0.75f + (random.NextFloat() - random.NextFloat()) * random.NextFloat() * 4.0f;
            if (currentStep == splitPoint && thickness > 1.0f)
            {
                //At the split point the original tunnel ends and two new ones branch left and right
                CreateTunnel(context, config, chunk, biomeGetter, random.NextLong(), aquifer, x, y, z,
                    horizontalRadiusMultiplier, verticalRadiusMultiplier, random.NextFloat() * 0.5f + 0.5f,
                    horizontalRotation - 1.5707964f, verticalRotation / 3.0f, currentStep, dist, 1.0, mask,
                    skipChecker);
                CreateTunnel(context, config, chunk, biomeGetter, random.NextLong(), aquifer, x, y, z,
                    horizontalRadiusMultiplier, verticalRadiusMultiplier, random.NextFloat() * 0.5f + 0.5f,
                    horizontalRotation + 1.5707964f, verticalRotation / 3.0f, currentStep, dist, 1.0, mask,
                    skipChecker);
                return;
            }
            if (random.NextInt(4) == 0) continue;
            if (!CanReach(chunk.Pos, x, z, currentStep, dist, thickness)) return;
            CarveEllipsoid(context, config, chunk, biomeGetter, aquifer, x, y, z,
                horizontalRadius * horizontalRadiusMultiplier, verticalRadius * verticalRadiusMultiplier, mask,
                skipChecker);
        }
    }

    //ShouldSkip skip voxels outside the ellipsoid or below the floor level, maps to vanilla shouldSkip
    private static bool ShouldSkip(double xd, double yd, double zd, double floorLevel)
        => yd <= floorLevel || xd * xd + yd * yd + zd * zd >= 1.0;
}

//NetherWorldCarver nether cave carver, maps to vanilla NetherWorldCarver
//Caves are thicker and taller; below 31 blocks they fill with lava and surface material is not recomputed
public sealed class NetherWorldCarver : CaveWorldCarver
{
    public NetherWorldCarver(Identifier id) : base(id) { }

    protected override int GetCaveBound() => 10;

    protected override float GetThickness(RandomSource random)
        => (random.NextFloat() * 2.0f + random.NextFloat()) * 2.0f;

    protected override double GetYScale() => 5.0;

    protected override bool CarveBlock(CarvingContext context, CarverConfiguration config, ChunkAccess chunk,
        Func<int, int, int, Biome> biomeGetter, int x, int y, int z, Aquifer aquifer, ref bool hasGrass)
    {
        if (config is not CaveCarverConfiguration caveConfig)
            throw new ArgumentException($"nether cave carver requires {nameof(CaveCarverConfiguration)}: {config.GetType().Name}");
        if (!CanReplaceBlock(caveConfig, chunk.GetBlockState(x, y, z))) return false;
        var state = y <= context.GetMinGenY() + 31 ? Blocks.LavaFluidState.CreateLegacyBlock() : CaveAir;
        chunk.SetBlockState(x, y, z, state);
        return true;
    }
}

//CanyonWorldCarver canyon carver, maps to vanilla CanyonWorldCarver
//Push a wide, long ravine along a main axis; vertical thickness varies randomly with the height factor
public sealed class CanyonWorldCarver : WorldCarver
{
    public CanyonWorldCarver(Identifier id) : base(id) { }

    public override bool IsStartChunk(CarverConfiguration config, RandomSource random)
        => random.NextFloat() <= config.Probability;

    public override bool Carve(CarvingContext context, CarverConfiguration config, ChunkAccess chunk,
        Func<int, int, int, Biome> biomeGetter, RandomSource random, Aquifer aquifer, ChunkPos sourceChunkPos,
        CarvingMask mask)
    {
        if (config is not CanyonCarverConfiguration canyonConfig)
            throw new ArgumentException($"canyon carver requires {nameof(CanyonCarverConfiguration)}: {config.GetType().Name}");
        var maxDistance = (Range * 2 - 1) * 16;
        var x = (double)(sourceChunkPos.MinBlockX + random.NextInt(16));
        double y = canyonConfig.Y.Sample(random, context);
        var z = (double)(sourceChunkPos.MinBlockZ + random.NextInt(16));
        var horizontalRotation = random.NextFloat() * 6.2831855f;
        var verticalRotation = canyonConfig.VerticalRotation.Sample(random);
        var yScale = canyonConfig.YScale.Sample(random);
        var thickness = canyonConfig.Shape.Thickness.Sample(random);
        var distance = (int)(maxDistance * canyonConfig.Shape.DistanceFactor.Sample(random));
        DoCarve(context, canyonConfig, chunk, biomeGetter, random.NextLong(), aquifer, x, y, z, thickness,
            horizontalRotation, verticalRotation, 0, distance, yScale, mask);
        return true;
    }

    private void DoCarve(CarvingContext context, CanyonCarverConfiguration config, ChunkAccess chunk,
        Func<int, int, int, Biome> biomeGetter, long tunnelSeed, Aquifer aquifer, double x, double y, double z,
        float thickness, float horizontalRotation, float verticalRotation, int step, int distance, double yScale,
        CarvingMask mask)
    {
        var random = RandomSource.Create(tunnelSeed);
        var widthFactorPerHeight = InitWidthFactors(context, config, random);
        var yRota = 0.0f;
        var xRota = 0.0f;
        for (var currentStep = step; currentStep < distance; currentStep++)
        {
            var horizontalRadius = 1.5 + Mth.Sin(currentStep * 3.1415927f / distance) * thickness;
            var verticalRadius = horizontalRadius * yScale;
            var horizontalRadius2 = horizontalRadius * config.Shape.HorizontalRadiusFactor.Sample(random);
            var verticalRadius2 = UpdateVerticalRadius(config, random, verticalRadius, distance, currentStep);
            var xc = Mth.Cos(verticalRotation);
            var xs = Mth.Sin(verticalRotation);
            x += Mth.Cos(horizontalRotation) * xc;
            y += xs;
            z += Mth.Sin(horizontalRotation) * xc;
            verticalRotation = verticalRotation * 0.7f + xRota * 0.05f;
            horizontalRotation += yRota * 0.05f;
            xRota = xRota * 0.8f + (random.NextFloat() - random.NextFloat()) * random.NextFloat() * 2.0f;
            yRota = yRota * 0.5f + (random.NextFloat() - random.NextFloat()) * random.NextFloat() * 4.0f;
            if (random.NextInt(4) == 0) continue;
            if (!CanReach(chunk.Pos, x, z, currentStep, distance, thickness)) return;
            CarveEllipsoid(context, config, chunk, biomeGetter, aquifer, x, y, z, horizontalRadius2, verticalRadius2,
                mask, (ctx, xd, yd, zd, worldY) => ShouldSkip(ctx, widthFactorPerHeight, xd, yd, zd, worldY));
        }
    }

    //InitWidthFactors pick a random width factor per height and square it, maps to vanilla initWidthFactors
    private static float[] InitWidthFactors(CarvingContext context, CanyonCarverConfiguration config,
        RandomSource random)
    {
        var depth = context.GetGenDepth();
        var widthFactorPerHeight = new float[depth];
        var widthFactor = 1.0f;
        for (var yIndex = 0; yIndex < depth; yIndex++)
        {
            if (yIndex == 0 || random.NextInt(config.Shape.WidthSmoothness) == 0)
                widthFactor = 1.0f + random.NextFloat() * random.NextFloat();
            widthFactorPerHeight[yIndex] = widthFactor * widthFactor;
        }
        return widthFactorPerHeight;
    }

    //UpdateVerticalRadius canyon is thicker in the middle and thinner at both ends, maps to vanilla updateVerticalRadius
    private static double UpdateVerticalRadius(CanyonCarverConfiguration config, RandomSource random,
        double verticalRadius, int distance, int currentStep)
    {
        var verticalMultiplier = 1.0f - Mth.Abs(0.5f - currentStep / (float)distance) * 2.0f;
        var factor = config.Shape.VerticalRadiusDefaultFactor
            + config.Shape.VerticalRadiusCenterFactor * verticalMultiplier;
        return factor * verticalRadius * Mth.RandomBetween(random, 0.75f, 1.0f);
    }

    //ShouldSkip test the ellipsoid using the per-height width factor, maps to vanilla shouldSkip
    private static bool ShouldSkip(CarvingContext context, float[] widthFactorPerHeight, double xd, double yd,
        double zd, int y)
    {
        var yIndex = y - context.GetMinGenY();
        return (xd * xd + zd * zd) * widthFactorPerHeight[yIndex - 1] + yd * yd / 6.0 >= 1.0;
    }
}
