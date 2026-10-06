namespace NetCraft.Util.Random;

//Random source interface, maps to vanilla net.minecraft.util.RandomSource
//All random number generation entry points, defining fork and the various next methods
public interface RandomSource
{
    //fork derives a new independent random source, maps to vanilla fork
    RandomSource Fork();

    //forkPositional derives a positional factory, maps to vanilla forkPositional
    PositionalRandomFactory ForkPositional();

    //setSeed resets the seed, maps to vanilla setSeed
    void SetSeed(long seed);

    int NextInt();
    int NextInt(int bound);
    long NextLong();
    bool NextBoolean();
    float NextFloat();
    double NextDouble();
    double NextGaussian();

    //consumeCount consumes the given number of rounds, maps to vanilla consumeCount, default calls nextInt
    void ConsumeCount(int rounds)
    {
        for (var i = 0; i < rounds; i++)
            NextInt();
    }

    //nextIntBetweenInclusive closed-interval random integer, maps to vanilla nextIntBetweenInclusive
    int NextIntBetweenInclusive(int min, int maxInclusive)
        => NextInt(maxInclusive - min + 1) + min;

    //triangle triangular distribution, maps to vanilla triangle(double)
    double Triangle(double mean, double spread)
        => mean + spread * (NextDouble() - NextDouble());

    //triangle triangular distribution float overload
    float Triangle(float mean, float spread)
        => mean + spread * (NextFloat() - NextFloat());

    //nextInt with origin overload, maps to vanilla nextInt(origin,bound)
    int NextInt(int origin, int bound)
    {
        if (origin >= bound)
            throw new ArgumentException("bound - origin is non positive");
        return origin + NextInt(bound - origin);
    }

    //create default factory, maps to vanilla create, generates a unique seed
    static RandomSource Create() => Create(RandomSupport.GenerateUniqueSeed());

    //create builds a LegacyRandomSource from the seed as a placeholder; XoroshiroRandomSource and the like come later in the Legacy stage
    static RandomSource Create(long seed) => new XoroshiroRandomSource(seed);
}
