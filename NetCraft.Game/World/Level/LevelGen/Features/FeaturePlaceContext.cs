using NetCraft.Primitives;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Features;

//FeaturePlaceContext feature placement context, maps to vanilla FeaturePlaceContext
//Bundles the world, generator, random source, origin and decoded config for Feature.Place
public sealed class FeaturePlaceContext
{
    public WorldGenRegion Level { get; }
    public ChunkGenerator ChunkGenerator { get; }
    public RandomSource Random { get; }
    public BlockPos Origin { get; }
    public FeatureConfiguration Config { get; }

    public FeaturePlaceContext(WorldGenRegion level, ChunkGenerator chunkGenerator, RandomSource random,
        BlockPos origin, FeatureConfiguration config)
    {
        Level = level;
        ChunkGenerator = chunkGenerator;
        Random = random;
        Origin = origin;
        Config = config;
    }
}
