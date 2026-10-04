using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen;

//TheEndBiomeSource 末地生物群系源 对应原版 net.minecraft.world.level.biome.TheEndBiomeSource
//主岛同心圆范围内恒为 the_end 之外按 end_islands 噪声值分四档
//原版 ISLAND_CHUNK_DISTANCE_SQR 来自 NoiseRouterData 阈值 4096 即半径 64 区块
public sealed class TheEndBiomeSource : BiomeSource
{
    //IslandChunkDistanceSqr 主岛判定阈值 对应原版 NoiseRouterData.ISLAND_CHUNK_DISTANCE_SQR
    public const long IslandChunkDistanceSqr = 4096L;

    //HighlandsThreshold 高地下限 对应原版 heightValue > 0.25
    public const double HighlandsThreshold = 0.25;

    //MidlandsThreshold 中地下限 对应原版 heightValue >= -0.0625
    public const double MidlandsThreshold = -0.0625;

    //SmallIslandsThreshold 小岛上限 对应原版 heightValue < -0.21875
    public const double SmallIslandsThreshold = -0.21875;

    public Biome End { get; }
    public Biome Highlands { get; }
    public Biome Midlands { get; }
    public Biome SmallIslands { get; }
    public Biome Barrens { get; }

    //PossibleBiomes 末地源固定产出这五个群系 对应原版 TheEndBiomeSource.possibleBiomes
    public IReadOnlyList<Biome> PossibleBiomes => new[] { End, Highlands, Midlands, SmallIslands, Barrens };

    //ErosionNoise 末地的 end_islands 密度函数 对应原版 sampler.erosion()
    //装配时由 NoiseBasedChunkGenerator 按噪声设置的 erosion 槽注入 未注入时恒返回主岛群系
    public DensityFunction? ErosionNoise { get; }

    public TheEndBiomeSource(Biome end, Biome highlands, Biome midlands, Biome smallIslands, Biome barrens,
        DensityFunction? erosionNoise = null)
    {
        End = end;
        Highlands = highlands;
        Midlands = midlands;
        SmallIslands = smallIslands;
        Barrens = barrens;
        ErosionNoise = erosionNoise;
    }

    //WithErosion 注入 end_islands 密度函数 对应原版 create 时按 sampler 定形
    public TheEndBiomeSource WithErosion(DensityFunction erosionNoise)
        => new(End, Highlands, Midlands, SmallIslands, Barrens, erosionNoise);

    //FromRegistry 从群系注册表取末地五群系 对应原版 TheEndBiomeSource.create
    //缺任一必需群系返回 null 由调用方报错 数据包不完整时不该拿占位群系凑出一个假末地
    public static TheEndBiomeSource? FromRegistry(Registry<Biome>? registry)
    {
        if (registry is null) return null;
        var end = Get(registry, Biomes.THE_END);
        var highlands = Get(registry, Biomes.END_HIGHLANDS);
        var midlands = Get(registry, Biomes.END_MIDLANDS);
        var smallIslands = Get(registry, Biomes.SMALL_END_ISLANDS);
        var barrens = Get(registry, Biomes.END_BARRENS);
        if (end is null || highlands is null || midlands is null || smallIslands is null || barrens is null)
            return null;
        return new TheEndBiomeSource(end, highlands, midlands, smallIslands, barrens);
    }

    //GetBiome 传世界方块坐标 主岛判定要按区块坐标算
    //噪声采样点取区块中心 (chunkX*2+1)*8 与原版一致 逐区 4 格采样时同区块结果恒定
    public Biome GetBiome(int x, int y, int z)
    {
        var chunkX = x >> 4;
        var chunkZ = z >> 4;
        if ((long)chunkX * chunkX + (long)chunkZ * chunkZ <= IslandChunkDistanceSqr)
            return End;
        if (ErosionNoise is null) return End;
        var noiseX = ((chunkX * 2) + 1) * 8;
        var noiseZ = ((chunkZ * 2) + 1) * 8;
        var value = ErosionNoise.Compute(SinglePointContext.At(noiseX, y, noiseZ));
        if (value > HighlandsThreshold) return Highlands;
        if (value >= MidlandsThreshold) return Midlands;
        if (value < SmallIslandsThreshold) return SmallIslands;
        return Barrens;
    }

    private static Biome? Get(Registry<Biome> registry, ResourceKey<Biome> key)
        => registry.GetValue(key.Identifier);
}
