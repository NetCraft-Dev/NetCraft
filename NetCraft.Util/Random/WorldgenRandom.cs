namespace NetCraft.Util.Random;

//WorldgenRandom worldgen seed derivation helper, maps to vanilla net.minecraft.world.level.levelgen.WorldgenRandom
//Vanilla puts the derivation logic on a random source wrapper; this project provides static methods instead, with the underlying random source supplied by the caller
//Structure placement and decoration rely on these derivations to guarantee same seed same result; the formula and consumption order must match exactly or structure positions drift
//Note: structure placement uses LegacyRandomSource while decoration uses Xoroshiro; the two produce different values and must not be mixed
public static class WorldgenRandom
{
    //SetLargeFeatureSeed derives a seed from chunk coordinates, maps to vanilla setLargeFeatureSeed
    //Takes two odd scaling factors then XORs the chunk coordinates; structure placement and carvers share the same formula
    public static void SetLargeFeatureSeed(RandomSource random, long seed, int chunkX, int chunkZ)
    {
        random.SetSeed(seed);
        var xScale = random.NextLong() | 1L;
        var zScale = random.NextLong() | 1L;
        random.SetSeed(chunkX * xScale ^ chunkZ * zScale ^ seed);
    }

    //SetLargeFeatureWithSalt derives a salted seed, maps to vanilla setLargeFeatureWithSalt
    //Random scatter placement uses it to compute potential chunks in a grid; it is a pure linear combination and consumes no random numbers
    public static void SetLargeFeatureWithSalt(RandomSource random, long seed, int x, int z, int salt)
    {
        random.SetSeed(x * 341873128712L + z * 132897987541L + seed + salt);
    }

    //SetDecorationSeed derives and returns the decoration seed, maps to vanilla setDecorationSeed
    //Decoration and structure placement both use it as reference; the return value is fed into SetFeatureSeed
    public static long SetDecorationSeed(RandomSource random, long worldSeed, int minBlockX, int minBlockZ)
    {
        random.SetSeed(worldSeed);
        var xScale = random.NextLong() | 1L;
        var zScale = random.NextLong() | 1L;
        var seed = minBlockX * xScale ^ minBlockZ * zScale ^ worldSeed;
        random.SetSeed(seed);
        return seed;
    }

    //SetFeatureSeed derives a seed from the in-step index, maps to vanilla setFeatureSeed
    //Step size 10000 keeps adjacent indices within a step from overlapping; features and structures both use it
    public static void SetFeatureSeed(RandomSource random, long decorationSeed, int index, int step)
        => random.SetSeed(decorationSeed + index + 10000L * step);
}
