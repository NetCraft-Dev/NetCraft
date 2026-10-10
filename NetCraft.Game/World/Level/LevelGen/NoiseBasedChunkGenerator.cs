using System.Runtime.CompilerServices;
using NetCraft.Game.Bootstrap;
using NetCraft.Game.World.Level.Block;
using NetCraft.Game.World.Level.LevelGen.Carver;
using NetCraft.Game.World.Level.LevelGen.Synth;
using NetCraft.Logging;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Util.Random;
using GameConfiguredWorldCarver = NetCraft.Game.World.Level.LevelGen.Carver.ConfiguredWorldCarver;
using GameStructure = NetCraft.Game.World.Level.LevelGen.Structure.Structure;
using GameStructureFeatureManager = NetCraft.Game.World.Level.LevelGen.Structure.StructureFeatureManager;
using GameGenerationStep = NetCraft.Game.World.Level.LevelGen.Features.GenerationStep;
using GamePlacedFeature = NetCraft.Game.World.Level.LevelGen.Placement.PlacedFeature;
using GameBeardifier = NetCraft.Game.World.Level.LevelGen.Structure.Beardifier;

namespace NetCraft.Game.World.Level.LevelGen;

//NoiseBasedChunkGenerator is the noise-based chunk generator, maps to vanilla net.minecraft.world.level.levelgen.NoiseBasedChunkGenerator
//Extends ChunkGenerator and holds NoiseGeneratorSettings plus the base PerlinNoise
//P0 wires up RandomState + NoiseChunk + Aquifer and drives block decisions through GetInterpolatedState
//The kernel hardcodes no blocks; DefaultBlock/DefaultFluid are injected by the Game layer via NoiseGeneratorSettings
public class NoiseBasedChunkGenerator : ChunkGenerator
{
    public NoiseGeneratorSettings Settings { get; }

    //RandomState is created once per world seed and reused for the whole session, matching vanilla's world-level singleton semantics
    //The density tree mapAll and NormalNoise are expensive to instantiate; rebuilding per chunk is slow and would make neighboring chunks' noise discontinuous
    //volatile: the other half of the double-checked lock; without it another thread could see a non-null object that is not fully constructed
    private volatile RandomState? _cachedRandomState;
    private readonly object _randomStateLock = new();

    //One NoiseChunk per chunk; built during the noise stage and reused directly by the surface stage, maps to vanilla protoChunk.getOrCreateNoiseChunk
    //A weak table avoids leaking NoiseChunk after the chunk unloads
    private readonly ConditionalWeakTable<ChunkAccess, NoiseChunk> _noiseChunks = new();

    //SamplerHeightNoise is the NormalNoise placeholder for terrain base height sampling
    //The real wiring derives it from RandomState; here it is simplified to a nullable field
    public NormalNoise? HeightNoise { get; private set; }

    public NoiseBasedChunkGenerator(BiomeSource biomeSource, NoiseGeneratorSettings settings)
        : base(WithSamplerIfNeeded(biomeSource, settings))
    {
        Settings = settings;
    }

    //WithSamplerIfNeeded checks before construction whether BiomeSource is an implementation without an injected sampler
    //Multi-noise sources build a NoiseRouterSampler from Settings.NoiseRouter; the End source injects the density function from the erosion slot
    //Not injected when ParameterList is null because GetBiome still takes the Biome.Plains placeholder path
    private static BiomeSource WithSamplerIfNeeded(BiomeSource biomeSource, NoiseGeneratorSettings settings)
    {
        if (biomeSource is MultiNoiseBiomeSource { Sampler: null, ParameterList: not null } multi)
            return new MultiNoiseBiomeSource(multi.ParameterList!, new Climate.NoiseRouterSampler(settings.NoiseRouter));
        //The End's erosion slot reads end_islands and differs from the overworld semantics; take the slot straight from the noise settings
        if (biomeSource is TheEndBiomeSource { ErosionNoise: null } theEnd)
            return theEnd.WithErosion(settings.NoiseRouter.Erosion);
        return biomeSource;
    }

    //InitHeightNoise derives the NormalNoise instance used for terrain base height sampling
    //firstOctave/amplitudes align with the vanilla default config; the simplified version uses -3 and [1,1,1,1,1,1,1] as placeholders
    public void InitHeightNoise(RandomSource random)
    {
        Log.Debug($"InitHeightNoise entry random={random}");
        HeightNoise = new NormalNoise(random, -3, 1, 1, 1, 1, 1, 1, 1);
        //Log.Debug("InitHeightNoise exit");
    }

    //GetGenDepth returns the max build depth derived from settings, maps to vanilla getGenDepth
    //Uses Settings.NoiseSettings.Height to align with vanilla settings.height
    public override int GetGenDepth() => Settings.NoiseSettings.Height;

    //GetMinY returns the lowest Y from the noise settings, maps to vanilla getMinY
    public override int GetMinY() => Settings.NoiseSettings.MinY;

    //GetBaseHeight samples the base height at the given coordinates, maps to vanilla getBaseHeight
    //type is an int placeholder for HeightmapTypes; the simplified version samples HeightNoise
    public override int GetBaseHeight(int x, int z, int type, LevelHeightAccessor level, RandomSource random)
    {
        Log.Debug($"GetBaseHeight entry x={x} z={z} type={type} level={level} random={random}");
        if (HeightNoise is null) GetOrCreateRandomState();
        var value = HeightNoise!.GetValue(x, 0, z);
        var raw = (int)Math.Round(value * 32 + 64);
        var result = Math.Clamp(raw, level.MinBuildHeight, level.MaxBuildHeight - 1);
        Log.Debug($"GetBaseHeight exit result={result}");
        return result;
    }

    //GetBaseColumn samples the base column block states at the given coordinates, maps to vanilla getBaseColumn
    //Simplified to return an object[] of length SectionsCount*16 with all null entries, pending BlockState wiring
    public override object[] GetBaseColumn(int x, int z, LevelHeightAccessor level, RandomSource random)
    {
        Log.Debug($"GetBaseColumn entry x={x} z={z} level={level} random={random}");
        var column = new object?[level.SectionsCount * 16];
        var surfaceY = GetBaseHeight(x, z, 0, level, random);
        for (var y = 0; y < column.Length; y++)
        {
            var absoluteY = level.MinBuildHeight + y;
            column[y] = absoluteY < surfaceY - 4 ? "stone" : null;
        }
        var result = column!;
        Log.Debug($"GetBaseColumn exit result={result.Length}");
        return result;
    }

    //CreateFluidPicker builds the global fluid picker, maps to vanilla NoiseBasedChunkGenerator.createFluidPicker
    //lavaStatus: deep lava for y<-54; seaStatus: sea-level fluid for y<seaLevel returns water
    //emptyStatus: AIR placeholder as a defensive fallback; vanilla uses DimensionType.MIN_Y*2, here int.MinValue/2 is more extreme
    //FluidPicker is the injection point for Game layer blocks; Blocks.LAVA/AIR are provided by the Game layer
    public static Aquifer.FluidPicker CreateFluidPicker(NoiseGeneratorSettings settings)
    {
        var lavaStatus = new FluidStatus(-54, Blocks.LAVA.DefaultBlockState);
        var seaLevel = settings.SeaLevel;
        var seaStatus = new FluidStatus(seaLevel, settings.DefaultFluid);
        var emptyStatus = new FluidStatus(int.MinValue / 2, Blocks.AIR.DefaultBlockState);
        return new GlobalFluidPicker(lavaStatus, seaStatus, emptyStatus, seaLevel);
    }

    //FillFromNoise fills blocks into the chunk from noise, maps to vanilla fillFromNoise
    //Streams cell by cell: corners are sampled once, in-cell blocks get their density from interpolated nodes
    //When density>0 the Aquifer returns null and Settings.DefaultBlock (stone) is the fallback
    //When density<=0 the Aquifer returns a fluid status: water below sea level, lava deeper, otherwise air
    public override void FillFromNoise(object blender, object structures, ChunkAccess chunk, RandomSource random)
    {
        if (chunk is not ProtoChunk proto) return;

        //The first call triggers Game layer Bootstrap to register blocks and noise params; later calls return idempotently
        GameBootstrap.Bootstrap();

        var randomState = GetOrCreateRandomState();
        //Structures are already assembled in the STRUCTURE_START stage; the noise stage uses them to flatten terrain within structure bounds
        var beardifier = structures is GameStructureFeatureManager manager
            ? GameBeardifier.ForStructuresInChunk(manager, chunk.Pos)
            : GameBeardifier.Empty;
        //Temporary instrumentation, splits the noise stage so an allocation type can be placed on the chunk build or the fill loop
        var marker = GC.GetTotalAllocatedBytes();
        var noiseChunk = GetOrCreateNoiseChunk(chunk, randomState, beardifier);
        NetCraft.Util.SiteCounters.Stage("noise.create", GC.GetTotalAllocatedBytes() - marker);
        var defaultBlock = Settings.DefaultBlock;

        var cellWidth = noiseChunk.CellWidth;
        var cellHeight = noiseChunk.CellHeight;
        var cellMinY = noiseChunk.CellNoiseMinY;
        var cellCountY = noiseChunk.CellCountY;
        var cellCountXZ = 16 / cellWidth;
        var chunkStartBlockX = chunk.Pos.MinBlockX;
        var chunkStartBlockZ = chunk.Pos.MinBlockZ;

        noiseChunk.InitializeForFirstCellX();
        marker = GC.GetTotalAllocatedBytes();
        for (var cellXIndex = 0; cellXIndex < cellCountXZ; cellXIndex++)
        {
            noiseChunk.AdvanceCellX(cellXIndex);
            for (var cellZIndex = 0; cellZIndex < cellCountXZ; cellZIndex++)
            {
                for (var cellYIndex = cellCountY - 1; cellYIndex >= 0; cellYIndex--)
                {
                    noiseChunk.SelectCellYZ(cellYIndex, cellZIndex);
                    for (var yInCell = cellHeight - 1; yInCell >= 0; yInCell--)
                    {
                        var posY = (cellMinY + cellYIndex) * cellHeight + yInCell;
                        var sectionY = posY >> 4;
                        noiseChunk.UpdateForY(posY, yInCell / (double)cellHeight);
                        for (var xInCell = 0; xInCell < cellWidth; xInCell++)
                        {
                            var posX = chunkStartBlockX + cellXIndex * cellWidth + xInCell;
                            noiseChunk.UpdateForX(posX, xInCell / (double)cellWidth);
                            for (var zInCell = 0; zInCell < cellWidth; zInCell++)
                            {
                                var posZ = chunkStartBlockZ + cellZIndex * cellWidth + zInCell;
                                noiseChunk.UpdateForZ(posZ, zInCell / (double)cellWidth);
                                var state = noiseChunk.GetInterpolatedState() ?? defaultBlock;
                                proto.SetBlockState(sectionY, posX & 15, posY & 15, posZ & 15, state);
                            }
                        }
                    }
                }
            }
            noiseChunk.SwapSlices();
        }
        NetCraft.Util.SiteCounters.Stage("noise.fill", GC.GetTotalAllocatedBytes() - marker);
        noiseChunk.StopInterpolation();
    }

    //GetOrCreateRandomState creates and caches RandomState by world seed, maps to vanilla's world-level singleton
    //The seed uses WorldSeed, not the caller-supplied random source: that one is a per-chunk derived local source
    //Whoever wins the init race fixes the whole session's terrain; the same world seed would otherwise yield different surface heights on each run
    private RandomState GetOrCreateRandomState()
    {
        var cached = _cachedRandomState;
        if (cached is not null) return cached;
        lock (_randomStateLock)
        {
            if (_cachedRandomState is null)
            {
                _cachedRandomState = RandomState.Create(Settings, BuiltInRegistries.NOISE, WorldSeed);
                //Height noise is likewise derived from the world seed; building it from the passed-in random source would drift with thread scheduling under concurrency
                InitHeightNoise(RandomSource.Create(WorldSeed));
            }
            return _cachedRandomState;
        }
    }

    //FindSpawnPosition does a radial climate search using the noise settings' spawn_target, maps to vanilla Climate.Sampler.findSpawnPosition
    //Returns null when the datapack provides no spawn_target; the caller falls back to the default spawn
    public BlockPos? FindSpawnPosition()
    {
        if (Settings.SpawnTarget.Count == 0) return null;
        var result = Climate.FindSpawnPosition(Settings.SpawnTarget, GetOrCreateRandomState().Sampler);
        Log.Debug($"FindSpawnPosition exit result={result}");
        return result;
    }

    //GetOrCreateNoiseChunk returns the chunk's NoiseChunk, creating one if absent, maps to vanilla getOrCreateNoiseChunk
    private NoiseChunk GetOrCreateNoiseChunk(ChunkAccess chunk, RandomState randomState, DensityFunction beardifier)
    {
        if (_noiseChunks.TryGetValue(chunk, out var existing)) return existing;
        var created = new NoiseChunk(chunk, randomState, Settings, beardifier);
        _noiseChunks.Add(chunk, created);
        return created;
    }

    //BuildSurface applies surface rules to the chunk, maps to vanilla buildSurface
    //The rule tree comes from settings' surface_rule; it decides both bedrock and surface material, skipped entirely when absent
    public override void BuildSurface(object region, object structures, ChunkAccess chunk, RandomSource random)
    {
        Log.Debug($"BuildSurface entry chunk={chunk.Pos} random={random}");
        GameBootstrap.Bootstrap();
        var ruleSource = Settings.SurfaceRule;
        if (ruleSource is null)
        {
            Log.Debug("BuildSurface exit, no surface_rule");
            return;
        }
        var randomState = GetOrCreateRandomState();
        //The noise stage already built a NoiseChunk from structures, so this usually hits the cache; a zero marker is used on the fallback path
        var marker = GC.GetTotalAllocatedBytes();
        var noiseChunk = GetOrCreateNoiseChunk(chunk, randomState, BeardifierMarker.Instance);
        NetCraft.Util.SiteCounters.Stage("surface.create", GC.GetTotalAllocatedBytes() - marker);
        //Surface-stage biome lookups go through BiomeManager: a palette hit in this chunk is a single table lookup, only out-of-bounds falls back to full sampling
        //Passing GetBiome directly would run a 6D climate sample per block and slow chunk generation to the point of connection timeouts
        marker = GC.GetTotalAllocatedBytes();
        var biomeManager = new BiomeManager(chunk, GetBiome, randomState.Seed);
        NetCraft.Util.SiteCounters.Stage("surface.biome", GC.GetTotalAllocatedBytes() - marker);
        marker = GC.GetTotalAllocatedBytes();
        randomState.SurfaceSystem.BuildSurface(chunk, noiseChunk, ruleSource, biomeManager.GetBiome,
            chunk.MinSectionY * 16, GetGenDepth(), Settings.UseLegacyRandomSource);
        NetCraft.Util.SiteCounters.Stage("surface.rules", GC.GetTotalAllocatedBytes() - marker);
        //Log.Debug("BuildSurface exit");
    }

    //ApplyCarvers carves caves after surface and before decoration, maps to vanilla applyCarvers
    //Iterates the 17x17 chunks around the center chunk only to read each position's biome config; the carve target and mask are always the center chunk
    public override void ApplyCarvers(long seed, ChunkAccess chunk, RandomSource random)
    {
        if (chunk is not ProtoChunk proto) return;
        GameBootstrap.Bootstrap();
        var randomState = GetOrCreateRandomState();
        var noiseChunk = GetOrCreateNoiseChunk(chunk, randomState, BeardifierMarker.Instance);
        var context = new CarvingContext(this, chunk, noiseChunk, randomState, Settings.SurfaceRule);
        var mask = proto.GetOrCreateCarvingMask();
        Func<int, int, int, Biome> biomeGetter = GetBiome;
        var carverRandom = RandomSource.Create(seed);
        var pos = chunk.Pos;
        for (var dx = -8; dx <= 8; dx++)
        {
            for (var dz = -8; dz <= 8; dz++)
            {
                var sourcePos = new ChunkPos(pos.X + dx, pos.Z + dz);
                var carvers = BiomeSource.GetBiome(sourcePos.MinBlockX, 0, sourcePos.MinBlockZ).Generation.Carvers;
                var index = 0;
                foreach (var holder in carvers)
                {
                    if (holder.IsBound())
                    {
                        var carver = (GameConfiguredWorldCarver)holder.Value;
                        SetLargeFeatureSeed(carverRandom, seed + index, sourcePos.X, sourcePos.Z);
                        if (carver.IsStartChunk(carverRandom))
                            carver.Carve(context, chunk, biomeGetter, carverRandom, noiseChunk.Aquifer, sourcePos, mask);
                    }
                    index++;
                }
            }
        }
    }

    //SetLargeFeatureSeed reseeds the random source by chunk coordinates, maps to vanilla WorldgenRandom.setLargeFeatureSeed
    private static void SetLargeFeatureSeed(RandomSource random, long seed, int chunkX, int chunkZ)
    {
        random.SetSeed(seed);
        var xScale = random.NextLong() | 1L;
        var zScale = random.NextLong() | 1L;
        random.SetSeed(chunkX * xScale ^ chunkZ * zScale ^ seed);
    }

    //ApplyBiomeDecoration applies biome decoration, maps to vanilla applyBiomeDecoration
    //Advances through the 11 decoration steps; each step places structures first then features, since features need structures in place to grow on
    //Only handles features that actually appear within the 3x3 area and belong to this source's possible biomes
    public override void ApplyBiomeDecoration(WorldGenRegion region, ChunkAccess chunk,
        GameStructureFeatureManager structures)
    {
        var centerPos = chunk.Pos;
        var origin = new BlockPos(centerPos.X * 16, region.MinSectionY * 16, centerPos.Z * 16);
        var featureList = FeaturesPerStep;
        var random = new XoroshiroRandomSource(RandomSupport.GenerateUniqueSeed());
        var decorationSeed = WorldgenRandom.SetDecorationSeed(random, region.Seed, origin.X, origin.Z);

        //Enumerate the biome palettes of every section in the 3x3 and filter by this source's possible biomes
        //A neighbor may belong to another terrain source; this chunk must not decorate on its behalf
        var biomes = new HashSet<Biome>(ReferenceEqualityComparer.Instance);
        for (var offsetX = -1; offsetX <= 1; offsetX++)
        for (var offsetZ = -1; offsetZ <= 1; offsetZ++)
        {
            var neighbour = region.GetChunk(centerPos.X + offsetX, centerPos.Z + offsetZ);
            if (neighbour is null) continue;
            for (var sectionIndex = 0; sectionIndex < neighbour.SectionsCount; sectionIndex++)
            {
                neighbour.GetSection(neighbour.MinSectionY + sectionIndex)?.GetBiomes()
                    .GetAll(holder => { if (holder.IsBound()) biomes.Add(holder.Value); });
            }
        }
        biomes.IntersectWith(BiomeSource.PossibleBiomes);

        var structuresByStep = GroupStructuresByStep();
        var featureStepCount = featureList.Count;
        var generationSteps = Math.Max(GameGenerationStep.Count, featureStepCount);
        for (var stepIndex = 0; stepIndex < generationSteps; stepIndex++)
        {
            //For structures setFeatureSeed is fed the in-step ordinal, for features the in-step global index; the two differ semantically
            var index = 0;
            if (structures.ShouldGenerateStructures
                && structuresByStep.TryGetValue(stepIndex, out var stepStructures))
            {
                foreach (var structureId in stepStructures)
                {
                    WorldgenRandom.SetFeatureSeed(random, decorationSeed, index, stepIndex);
                    foreach (var start in structures.StartsForStructure(centerPos, structureId))
                        start.PlaceInChunk(region, centerPos.X, centerPos.Z);
                    index++;
                }
            }

            if (stepIndex >= featureStepCount) continue;
            var stepData = featureList[stepIndex];
            //Collect the global indices to place this step, then dedupe and sort; the order must match the sorter's topological order
            var candidates = new SortedSet<int>();
            foreach (var biome in biomes)
            {
                var featuresInBiome = biome.Generation.Features;
                if (stepIndex >= featuresInBiome.Count) continue;
                foreach (var holder in featuresInBiome[stepIndex])
                {
                    if (!holder.IsBound()) continue;
                    var globalIndex = stepData.IndexOf(holder.Value);
                    if (globalIndex >= 0) candidates.Add(globalIndex);
                }
            }
            foreach (var globalIndex in candidates)
            {
                if (stepData.Features[globalIndex] is not GamePlacedFeature placed) continue;
                WorldgenRandom.SetFeatureSeed(random, decorationSeed, globalIndex, stepIndex);
                placed.PlaceWithBiomeCheck(region, this, random, origin);
            }
        }
    }

    //GroupStructuresByStep groups loaded structure registry names by decoration step, maps to vanilla structuresByStep
    //The registry is immutable after freezing, so rebuilding each decoration matches vanilla's per-chunk regrouping behavior
    private static Dictionary<int, List<Identifier>> GroupStructuresByStep()
    {
        var result = new Dictionary<int, List<Identifier>>();
        foreach (var holder in BuiltInRegistries.STRUCTURE.ListElements())
        {
            if (holder.Value is not GameStructure structure) continue;
            var step = (int)structure.Settings.Step;
            if (!result.TryGetValue(step, out var list)) result[step] = list = new List<Identifier>();
            list.Add(structure.Id);
        }
        return result;
    }

    //GetBaseHeight is the legacy simplified signature kept for tests and simple callers
    //The internal delegation uses GetBaseHeight(int,int,int,LevelHeightAccessor,RandomSource) with type=0 and a placeholder accessor
    public int GetBaseHeight(int x, int z)
    {
        Log.Debug($"GetBaseHeight entry x={x} z={z}");
        if (HeightNoise is null)
        {
            Log.Debug("GetBaseHeight exit result=0, HeightNoise is empty");
            return 0;
        }
        var value = HeightNoise.GetValue(x, 0, z);
        var result = (int)Math.Round(value * 32 + 64);
        Log.Debug($"GetBaseHeight exit result={result}");
        return result;
    }

    //GetBiome delegates to BiomeSource for biome lookup
    public Biome GetBiome(int x, int y, int z)
        => BiomeSource.GetBiome(x, y, z);

    //GlobalFluidPicker is the inner implementation of the global fluid picker, maps to the lambda in vanilla createFluidPicker
    //y < min(-54, seaLevel) returns lavaStatus, otherwise seaStatus
    private sealed class GlobalFluidPicker : Aquifer.FluidPicker
    {
        private readonly FluidStatus _lavaStatus;
        private readonly FluidStatus _seaStatus;
        private readonly FluidStatus _emptyStatus;
        private readonly int _seaLevel;

        public GlobalFluidPicker(FluidStatus lava, FluidStatus sea, FluidStatus empty, int seaLevel)
        {
            _lavaStatus = lava;
            _seaStatus = sea;
            _emptyStatus = empty;
            _seaLevel = seaLevel;
        }

        public FluidStatus ComputeFluid(int blockX, int blockY, int blockZ)
        {
            if (blockY < Math.Min(-54, _seaLevel))
                return _lavaStatus;
            return _seaStatus;
        }
    }
}
