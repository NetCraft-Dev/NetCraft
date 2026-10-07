using NetCraft.Game.World.Level.LevelGen.Features;
using NetCraft.Game.World.Level.LevelGen.Structure;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen;

//ChunkGenerator abstract base class for chunk generators, maps to vanilla net.minecraft.world.level.chunk.ChunkGenerator
//Holds a BiomeSource; subclasses implement noise/surface/height/column sampling as needed
//Signatures of BuildSurface/FillFromNoise and others align with vanilla; placeholder parameters use object until WorldGenRegion/StructureManager are ready
public abstract class ChunkGenerator
{
    public BiomeSource BiomeSource { get; }

    //WorldSeed world seed injected when the generation chain is assembled, matches the seed vanilla createState receives
    //The noise random state must be built from it: a per-chunk local random source cannot serve as the seed or whichever chunk initialises first would decide the whole terrain
    public long WorldSeed { get; set; }

    protected ChunkGenerator(BiomeSource biomeSource)
    {
        BiomeSource = biomeSource;
    }

    //GetGenDepth maximum generation depth, maps to vanilla getGenDepth
    //The total chunk height from lowest to highest; subclasses derive it from settings
    public abstract int GetGenDepth();

    //GetMinY lowest block Y of the generation range, maps to vanilla getMinY
    //WorldGenerationContext uses it to resolve anchors into absolute Y
    public abstract int GetMinY();

    //GetBaseHeight samples the base height at a coordinate, maps to vanilla getBaseHeight
    //type is HeightmapTypes, a placeholder int until the Heightmap subsystem is ready
    public abstract int GetBaseHeight(int x, int z, int type, LevelHeightAccessor level, RandomSource random);

    //GetBaseColumn samples the base column block states at a coordinate, maps to vanilla getBaseColumn
    //Returns BlockState[]; simplified to object[] until BlockState is ready in the Game layer
    public abstract object[] GetBaseColumn(int x, int z, LevelHeightAccessor level, RandomSource random);

    //FillFromNoise fills blocks into the chunk from noise, maps to vanilla fillFromNoise
    //blender/structures are placeholder objects until those subsystems are ready
    public abstract void FillFromNoise(object blender, object structures, ChunkAccess chunk, RandomSource random);

    //BuildSurface applies surface rules to the chunk, maps to vanilla buildSurface
    //region/structures are placeholder objects until those subsystems are ready
    public abstract void BuildSurface(object region, object structures, ChunkAccess chunk, RandomSource random);

    //ApplyCarvers carves caves after the surface and before decoration, maps to vanilla applyCarvers
    //seed and index decide each carver's starting randomness; the implementation derives randomState from random itself
    public abstract void ApplyCarvers(long seed, ChunkAccess chunk, RandomSource random);

    //ApplyBiomeDecoration applies biome decoration, maps to vanilla applyBiomeDecoration
    //region is the generation region covering 3x3 chunks; structures collects the structure results assembled by this pipeline
    public abstract void ApplyBiomeDecoration(WorldGenRegion region, ChunkAccess chunk,
        StructureFeatureManager structures);

    //FeaturesPerStep per-step feature table sorted by global index, built from the possible biomes and cached on first access
    //The ordering keeps placement consistent for the same position across different chunk viewpoints, or the same seed would grow different terrain
    private IReadOnlyList<FeatureSorter.StepFeatureData>? _featuresPerStep;

    public IReadOnlyList<FeatureSorter.StepFeatureData> FeaturesPerStep
        => _featuresPerStep ??= FeatureSorter.BuildFeaturesPerStep(
            BiomeSource.PossibleBiomes, biome => biome.Generation.Features);
}
