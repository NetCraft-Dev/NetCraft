using NetCraft.Game.World.Level.Block;
using NetCraft.Game.World.Level.LevelGen.Synth;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Util;
using NetCraft.Util.Random;
using HeightmapRegistry = NetCraft.Registry.Heightmap;

namespace NetCraft.Game.World.Level.LevelGen;

//SurfaceSystem surface building system, maps to vanilla net.minecraft.world.level.levelgen.SurfaceSystem
//Scans each column top-down by surface_rule, replacing only the default block (stone); maintains stone depth and water height per cell for condition evaluation
//Default block/sea level are injected by NoiseGeneratorSettings and noise instances come from RandomState
public sealed class SurfaceSystem
{
    //WayBelowMinY sentinel below the minimum build height, maps to vanilla DimensionType.WAY_BELOW_MIN_Y
    private const int WayBelowMinY = -2032;

    //Biomes the extension rules target; vanilla compares concrete registry names, not tags
    private static readonly Identifier ErodedBadlandsId = Identifier.WithDefaultNamespace("eroded_badlands");
    private static readonly Identifier FrozenOceanId = Identifier.WithDefaultNamespace("frozen_ocean");
    private static readonly Identifier DeepFrozenOceanId = Identifier.WithDefaultNamespace("deep_frozen_ocean");

    private readonly BlockState _defaultBlock;
    private readonly BlockState[]? _clayBands;
    private readonly NormalNoise? _clayBandsOffsetNoise;
    private readonly NormalNoise? _surfaceNoise;
    private readonly NormalNoise? _surfaceSecondaryNoise;
    private readonly NormalNoise? _badlandsPillarNoise;
    private readonly NormalNoise? _badlandsPillarRoofNoise;
    private readonly NormalNoise? _badlandsSurfaceNoise;
    private readonly NormalNoise? _icebergPillarNoise;
    private readonly NormalNoise? _icebergPillarRoofNoise;
    private readonly NormalNoise? _icebergSurfaceNoise;

    private readonly BlockState _whiteTerracotta;
    private readonly BlockState _orangeTerracotta;
    private readonly BlockState _terracotta;
    private readonly BlockState _yellowTerracotta;
    private readonly BlockState _brownTerracotta;
    private readonly BlockState _redTerracotta;
    private readonly BlockState _lightGrayTerracotta;
    private readonly BlockState _packedIce;
    private readonly BlockState _snowBlock;
    private readonly PositionalRandomFactory? _noiseRandom;

    public int SeaLevel { get; }
    public RandomState? RandomState { get; }

    //SurfaceSystem production constructor with all noise and band tables ready
    public SurfaceSystem(RandomState randomState, BlockState defaultBlock, int seaLevel,
        PositionalRandomFactory noiseRandom)
    {
        RandomState = randomState;
        _defaultBlock = defaultBlock;
        SeaLevel = seaLevel;
        _noiseRandom = noiseRandom;

        _whiteTerracotta = StateOf("white_terracotta");
        _orangeTerracotta = StateOf("orange_terracotta");
        _terracotta = StateOf("terracotta");
        _yellowTerracotta = StateOf("yellow_terracotta");
        _brownTerracotta = StateOf("brown_terracotta");
        _redTerracotta = StateOf("red_terracotta");
        _lightGrayTerracotta = StateOf("light_gray_terracotta");
        _packedIce = StateOf("packed_ice");
        _snowBlock = StateOf("snow_block");

        _clayBandsOffsetNoise = randomState.GetOrCreateNoise(Noises.ClayBandsOffset);
        _surfaceNoise = randomState.GetOrCreateNoise(Noises.Surface);
        _surfaceSecondaryNoise = randomState.GetOrCreateNoise(Noises.SurfaceSecondary);
        _badlandsPillarNoise = randomState.GetOrCreateNoise(Noises.BadlandsPillar);
        _badlandsPillarRoofNoise = randomState.GetOrCreateNoise(Noises.BadlandsPillarRoof);
        _badlandsSurfaceNoise = randomState.GetOrCreateNoise(Noises.BadlandsSurface);
        _icebergPillarNoise = randomState.GetOrCreateNoise(Noises.IcebergPillar);
        _icebergPillarRoofNoise = randomState.GetOrCreateNoise(Noises.IcebergPillarRoof);
        _icebergSurfaceNoise = randomState.GetOrCreateNoise(Noises.IcebergSurface);
        _clayBands = GenerateBands(noiseRandom.FromHashOf("minecraft:clay_bands"));
    }

    //SurfaceSystem noise-free degraded constructor for building a Context in unit tests
    //Without noise surfaceDepth is 3, secondary noise is 0 and bands fall back to plain terracotta
    public SurfaceSystem(BlockState defaultBlock, int seaLevel)
    {
        _defaultBlock = defaultBlock;
        SeaLevel = seaLevel;
        _whiteTerracotta = defaultBlock;
        _orangeTerracotta = defaultBlock;
        _terracotta = defaultBlock;
        _yellowTerracotta = defaultBlock;
        _brownTerracotta = defaultBlock;
        _redTerracotta = defaultBlock;
        _lightGrayTerracotta = defaultBlock;
        _packedIce = defaultBlock;
        _snowBlock = defaultBlock;
    }

    //StateOf gets a block's default state by registry name; throws when the block registry lacks it, rather than silently generating the wrong material
    private static BlockState StateOf(string path)
    {
        var block = BuiltInRegistries.BLOCK.GetValue(Identifier.WithDefaultNamespace(path));
        return block is null
            ? throw new InvalidOperationException($"block registry is missing {path}")
            : block.DefaultBlockState;
    }

    //BuildSurface applies surface rules to a whole chunk, maps to vanilla SurfaceSystem.buildSurface
    //Scans each column top-down; an air column resets depth, fluid records water height, and stone accumulates depth and tries to replace the default block
    public void BuildSurface(ChunkAccess chunk, NoiseChunk? noiseChunk, SurfaceRules.RuleSource ruleSource,
        Func<int, int, int, Biome> biomeGetter, int minY, int height, bool useLegacyRandomSource)
    {
        var minBlockX = chunk.Pos.X * 16;
        var minBlockZ = chunk.Pos.Z * 16;
        var context = new SurfaceRules.Context(this, chunk, noiseChunk, biomeGetter, minY, height);
        var endY = minY;

        for (var x = 0; x < 16; x++)
        {
            for (var z = 0; z < 16; z++)
            {
                var blockX = minBlockX + x;
                var blockZ = minBlockZ + z;
                var startingHeight = chunk.GetHeight(HeightmapRegistry.Types.WorldSurfaceWg, x, z) + 1;
                var surfaceBiome = biomeGetter(blockX, useLegacyRandomSource ? 0 : startingHeight, blockZ);
                var surfaceBiomeId = surfaceBiome.Id;
                if (surfaceBiomeId == ErodedBadlandsId)
                    ErodedBadlandsExtension(chunk, blockX, blockZ, startingHeight, endY);

                //The badlands extension mutates blocks so the height must be re-read
                var columnHeight = chunk.GetHeight(HeightmapRegistry.Types.WorldSurfaceWg, x, z) + 1;
                context.UpdateXZ(blockX, blockZ);
                var stoneAboveDepth = 0;
                var waterHeight = int.MinValue;
                var nextCeilingStoneY = int.MaxValue;

                for (var y = columnHeight; y >= endY; y--)
                {
                    var old = chunk.GetBlockState(blockX, y, blockZ);
                    if (IsAir(old))
                    {
                        stoneAboveDepth = 0;
                        waterHeight = int.MinValue;
                        continue;
                    }
                    if (IsFluid(old))
                    {
                        if (waterHeight == int.MinValue) waterHeight = y + 1;
                        continue;
                    }
                    if (nextCeilingStoneY >= y)
                    {
                        //Walks down to the next stone ceiling segment, maps to vanilla lookahead
                        nextCeilingStoneY = WayBelowMinY;
                        for (var lookaheadY = y - 1; lookaheadY >= endY - 1; lookaheadY--)
                        {
                            if (IsStone(chunk.GetBlockState(blockX, lookaheadY, blockZ))) continue;
                            nextCeilingStoneY = lookaheadY + 1;
                            break;
                        }
                    }
                    stoneAboveDepth++;
                    var stoneBelowDepth = y - nextCeilingStoneY + 1;
                    context.UpdateY(stoneAboveDepth, stoneBelowDepth, waterHeight, y);
                    if (old != _defaultBlock) continue;
                    var replaced = ruleSource.Apply(context, old);
                    if (replaced is not null) SetBlock(chunk, blockX, y, blockZ, replaced.Value);
                }

                if (surfaceBiomeId == FrozenOceanId || surfaceBiomeId == DeepFrozenOceanId)
                    FrozenOceanExtension(context.GetMinSurfaceLevel(), surfaceBiome, chunk, blockX, blockZ,
                        startingHeight, endY);
            }
        }
    }

    //TopMaterial recomputes a cell's top material, maps to vanilla topMaterial
    //After carving cuts down to grass, the cell below must be re-decided as dirt or stone by the surface rule
    public BlockState? TopMaterial(SurfaceRules.RuleSource ruleSource, ChunkAccess chunk, NoiseChunk? noiseChunk,
        Func<int, int, int, Biome> biomeGetter, int minY, int height,
        int blockX, int blockY, int blockZ, bool underFluid)
    {
        var context = new SurfaceRules.Context(this, chunk, noiseChunk, biomeGetter, minY, height);
        context.UpdateXZ(blockX, blockZ);
        context.UpdateY(1, 1, underFluid ? int.MinValue : blockY, blockY);
        return ruleSource.Apply(context, chunk.GetBlockState(blockX, blockY, blockZ));
    }

    //GetSurfaceDepth surface thickness noise, maps to vanilla getSurfaceDepth; the noise maps to 2..5 blocks with positional randomness added
    public int GetSurfaceDepth(int blockX, int blockZ)
    {
        if (_surfaceNoise is null || _noiseRandom is null) return 3;
        var noiseValue = _surfaceNoise.GetValue(blockX, 0.0, blockZ);
        return (int)(noiseValue * 2.75 + 3.0 + _noiseRandom.At(blockX, 0, blockZ).NextDouble() * 0.25);
    }

    //GetSurfaceSecondary secondary surface noise, maps to vanilla getSurfaceSecondary
    public double GetSurfaceSecondary(int blockX, int blockZ)
        => _surfaceSecondaryNoise?.GetValue(blockX, 0.0, blockZ) ?? 0.0;

    //GetBand badlands band material, maps to vanilla getBand
    //Looks up the 192-entry band table by noise offset modulo; falls back to plain terracotta without noise
    public BlockState GetBand(int blockX, int blockY, int blockZ)
    {
        if (_clayBands is null || _clayBandsOffsetNoise is null) return _terracotta;
        var offset = (int)Math.Round(_clayBandsOffsetNoise.GetValue(blockX, 0.0, blockZ) * 4.0);
        return _clayBands[((blockY + offset) % _clayBands.Length + _clayBands.Length) % _clayBands.Length];
    }

    //ErodedBadlandsExtension eroded badlands pillars, maps to vanilla erodedBadlandsExtension
    //Adds tall pillars above the surface from pillar noise, only filling the default block where there was air
    private void ErodedBadlandsExtension(ChunkAccess chunk, int blockX, int blockZ, int height, int endY)
    {
        if (_badlandsSurfaceNoise is null || _badlandsPillarNoise is null || _badlandsPillarRoofNoise is null)
            return;
        var pillarBuffer = Math.Min(
            Math.Abs(_badlandsSurfaceNoise.GetValue(blockX, 0.0, blockZ) * 8.25),
            _badlandsPillarNoise.GetValue(blockX * 0.2, 0.0, blockZ * 0.2) * 15.0);
        if (pillarBuffer <= 0.0) return;
        var pillarFloor = Math.Abs(_badlandsPillarRoofNoise.GetValue(blockX * 0.75, 0.0, blockZ * 0.75) * 1.5);
        var extensionTop = 64.0 + Math.Min(pillarBuffer * pillarBuffer * 2.5,
            Math.Ceiling(pillarFloor * 50.0) + 24.0);
        var startY = Mth.Floor(extensionTop);
        if (height > startY) return;

        //Abandon if the pillar base hits bedrock or meets water, so the water surface is not filled with stone
        for (var y = startY; y >= endY; y--)
        {
            var oldState = chunk.GetBlockState(blockX, y, blockZ);
            if (oldState.Owner == _defaultBlock.Owner) break;
            if (IsFluid(oldState)) return;
        }
        for (var y = startY; y >= endY && IsAir(chunk.GetBlockState(blockX, y, blockZ)); y--)
            SetBlock(chunk, blockX, y, blockZ, _defaultBlock);
    }

    //FrozenOceanExtension frozen ocean icebergs, maps to vanilla frozenOceanExtension
    //Adds packed ice and snow above and below the water surface from iceberg noise; returns immediately without noise
    private void FrozenOceanExtension(int minSurfaceLevel, Biome surfaceBiome, ChunkAccess chunk,
        int blockX, int blockZ, int height, int endY)
    {
        if (_icebergSurfaceNoise is null || _icebergPillarNoise is null
            || _icebergPillarRoofNoise is null || _noiseRandom is null)
            return;
        var iceberg = Math.Min(
            Math.Abs(_icebergSurfaceNoise.GetValue(blockX, 0.0, blockZ) * 8.25),
            _icebergPillarNoise.GetValue(blockX * 1.28, 0.0, blockZ * 1.28) * 15.0);
        if (iceberg <= 1.8) return;
        var icebergRoof = Math.Abs(_icebergPillarRoofNoise.GetValue(blockX * 1.17, 0.0, blockZ * 1.17) * 1.5);
        var top = Math.Min(iceberg * iceberg * 1.2, Math.Ceiling(icebergRoof * 40.0) + 14.0);
        if (BiomeTemperature.ShouldMeltFrozenOceanIcebergSlightly(surfaceBiome, blockX, SeaLevel, blockZ, SeaLevel))
            top -= 2.0;

        double extensionBottom;
        double extensionTop;
        if (top > 2.0)
        {
            extensionBottom = SeaLevel - top - 7.0;
            extensionTop = top + SeaLevel;
        }
        else
        {
            extensionTop = 0.0;
            extensionBottom = 0.0;
        }

        var random = _noiseRandom.At(blockX, 0, blockZ);
        var maxSnowDepth = 2 + random.NextInt(4);
        var minSnowHeight = SeaLevel + 18 + random.NextInt(10);
        var snowDepth = 0;
        var from = Math.Max(height, (int)extensionTop + 1);
        for (var y = from; y >= Math.Max(minSurfaceLevel, endY); y--)
        {
            var state = chunk.GetBlockState(blockX, y, blockZ);
            var replaceAir = IsAir(state) && y < (int)extensionTop && random.NextDouble() > 0.01;
            var replaceWater = IsWater(state) && y > (int)extensionBottom && y < SeaLevel
                && extensionBottom != 0.0 && random.NextDouble() > 0.15;
            if (!replaceAir && !replaceWater) continue;
            if (snowDepth <= maxSnowDepth && y > minSnowHeight)
            {
                SetBlock(chunk, blockX, y, blockZ, _snowBlock);
                snowDepth++;
            }
            else
            {
                SetBlock(chunk, blockX, y, blockZ, _packedIce);
            }
        }
    }

    //GenerateBands pre-generates the 192-entry badlands band table, maps to vanilla generateBands
    //Fills with plain terracotta then randomly inserts orange/yellow/brown/red/white/light gray per layer; the result depends only on the noise random source
    private BlockState[] GenerateBands(RandomSource random)
    {
        var bands = new BlockState[192];
        Array.Fill(bands, _terracotta);
        var index = 0;
        while (index < bands.Length)
        {
            var next = index + random.NextInt(5) + 1;
            if (next < bands.Length) bands[next] = _orangeTerracotta;
            index = next + 1;
        }
        MakeBands(random, bands, 1, _yellowTerracotta);
        MakeBands(random, bands, 2, _brownTerracotta);
        MakeBands(random, bands, 1, _redTerracotta);

        var whiteBandCount = random.NextIntBetweenInclusive(9, 15);
        var placed = 0;
        var start = 0;
        while (placed < whiteBandCount && start < bands.Length)
        {
            bands[start] = _whiteTerracotta;
            if (start - 1 > 0 && random.NextBoolean()) bands[start - 1] = _lightGrayTerracotta;
            if (start + 1 < bands.Length && random.NextBoolean()) bands[start + 1] = _lightGrayTerracotta;
            placed++;
            start += random.NextInt(16) + 4;
        }
        return bands;
    }

    //MakeBands scatters several segments of the given colour, maps to vanilla makeBands
    private static void MakeBands(RandomSource random, BlockState[] bands, int baseWidth, BlockState state)
    {
        var bandCount = random.NextIntBetweenInclusive(6, 15);
        for (var i = 0; i < bandCount; i++)
        {
            var width = baseWidth + random.NextInt(3);
            var start = random.NextInt(bands.Length);
            for (var p = 0; start + p < bands.Length && p < width; p++)
                bands[start + p] = state;
        }
    }

    //SetBlock writes a block by world coordinate, ignoring anything outside the build height
    private static void SetBlock(ChunkAccess chunk, int blockX, int blockY, int blockZ, BlockState state)
    {
        var section = chunk.GetSection(blockY >> 4);
        section?.SetBlockState(blockX & 15, blockY & 15, blockZ & 15, state);
    }

    private static bool IsAir(BlockState state) => state.Owner is BlockBehaviour { IsAir: true };

    private static bool IsFluid(BlockState state) => state.Owner is BlockBehaviour { HasFluidState: true };

    private static bool IsWater(BlockState state) => state.Owner.Id.Path == "water";

    //IsStone counts anything that is neither air nor fluid as stone, maps to vanilla isStone
    private static bool IsStone(BlockState state) => !IsAir(state) && !IsFluid(state);
}
