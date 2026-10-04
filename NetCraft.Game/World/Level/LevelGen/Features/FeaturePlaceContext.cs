using NetCraft.Primitives;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Features;

//FeaturePlaceContext 特征放置上下文对应原版 FeaturePlaceContext
//把世界/生成器/随机源/原点/已解好的配置打包给 Feature.Place
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
