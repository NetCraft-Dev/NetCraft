using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen;

//TheEndBiomeSource the End biome source, maps to vanilla net.minecraft.world.level.biome.TheEndBiomeSource
//Always the_end within the concentric main island radius; outside it, four tiers by the end_islands noise value
//Vanilla ISLAND_CHUNK_DISTANCE_SQR comes from NoiseRouterData, threshold 4096, a radius of 64 chunks
public sealed class TheEndBiomeSource : BiomeSource
{
    //IslandChunkDistanceSqr main island threshold, maps to vanilla NoiseRouterData.ISLAND_CHUNK_DISTANCE_SQR
    public const long IslandChunkDistanceSqr = 4096L;

    //HighlandsThreshold highlands lower bound, maps to vanilla heightValue > 0.25
    public const double HighlandsThreshold = 0.25;

    //MidlandsThreshold midlands lower bound, maps to vanilla heightValue >= -0.0625
    public const double MidlandsThreshold = -0.0625;

    //SmallIslandsThreshold small islands upper bound, maps to vanilla heightValue < -0.21875
    public const double SmallIslandsThreshold = -0.21875;

    public Biome End { get; }
    public Biome Highlands { get; }
    public Biome Midlands { get; }
    public Biome SmallIslands { get; }
    public Biome Barrens { get; }

    //PossibleBiomes the End source always yields these five biomes, maps to vanilla TheEndBiomeSource.possibleBiomes
    public IReadOnlyList<Biome> PossibleBiomes => new[] { End, Highlands, Midlands, SmallIslands, Barrens };

    //ErosionNoise the End's end_islands density function, maps to vanilla sampler.erosion()
    //Injected by NoiseBasedChunkGenerator from the noise settings' erosion slot during assembly; without it the main island biome is always returned
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

    //WithErosion injects the end_islands density function, matching how vanilla create shapes it from the sampler
    public TheEndBiomeSource WithErosion(DensityFunction erosionNoise)
        => new(End, Highlands, Midlands, SmallIslands, Barrens, erosionNoise);

    //FromRegistry takes the five End biomes from the biome registry, maps to vanilla TheEndBiomeSource.create
    //Returns null if any required biome is missing and lets the caller report it; an incomplete data pack should not fake an End out of placeholder biomes
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

    //GetBiome takes world block coordinates; the main island test works on chunk coordinates
    //The noise sample point is the chunk centre (chunkX*2+1)*8, matching vanilla, so the result is constant within a chunk when sampling every 4 blocks
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
