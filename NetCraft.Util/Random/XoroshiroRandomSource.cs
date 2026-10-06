using System.Text;

namespace NetCraft.Util.Random;

//Xoroshiro random source main implementation, maps to vanilla net.minecraft.world.level.levelgen.XoroshiroRandomSource
//Wraps Xoroshiro128PlusPlus, provides the RandomSource interface and positional factory
public sealed class XoroshiroRandomSource : RandomSource
{
    //FLOAT_UNIT 24-bit float unit 2^-24, maps to vanilla FLOAT_UNIT
    private const float FloatUnit = 5.9604645E-8f;

    //DOUBLE_UNIT 53-bit double unit 2^-53, maps to vanilla DOUBLE_UNIT
    private const double DoubleUnit = 1.1102230246251565E-16d;

    private Xoroshiro128PlusPlus _randomNumberGenerator;
    private readonly MarsagliaPolarGaussian _gaussianSource;

    //Constructs from a single long seed, maps to vanilla XoroshiroRandomSource(long)
    //Upgrades a single long seed to 128 bits to ensure the state space
    public XoroshiroRandomSource(long seed)
    {
        _randomNumberGenerator = new Xoroshiro128PlusPlus(RandomSupport.UpgradeSeedTo128bit(seed));
        _gaussianSource = new MarsagliaPolarGaussian(this);
    }

    //Constructs from Seed128bit, maps to vanilla XoroshiroRandomSource(Seed128bit)
    public XoroshiroRandomSource(RandomSupport.Seed128bit seed)
    {
        _randomNumberGenerator = new Xoroshiro128PlusPlus(seed);
        _gaussianSource = new MarsagliaPolarGaussian(this);
    }

    //Constructs from two longs, maps to vanilla XoroshiroRandomSource(long,long)
    public XoroshiroRandomSource(long seedLo, long seedHi)
    {
        _randomNumberGenerator = new Xoroshiro128PlusPlus(seedLo, seedHi);
        _gaussianSource = new MarsagliaPolarGaussian(this);
    }

    //fork derives a new random source, maps to vanilla fork
    //Uses two nextLong calls from the current generator as the new seed to avoid correlation
    public RandomSource Fork()
        => new XoroshiroRandomSource(_randomNumberGenerator.NextLong(), _randomNumberGenerator.NextLong());

    //forkPositional derives a positional factory, maps to vanilla forkPositional
    public PositionalRandomFactory ForkPositional()
        => new XoroshiroPositionalRandomFactory(_randomNumberGenerator.NextLong(), _randomNumberGenerator.NextLong());

    //setSeed resets the seed and clears the Gaussian cache, maps to vanilla setSeed
    public void SetSeed(long seed)
    {
        _randomNumberGenerator = new Xoroshiro128PlusPlus(RandomSupport.UpgradeSeedTo128bit(seed));
        _gaussianSource.Reset();
    }

    public int NextInt() => (int)_randomNumberGenerator.NextLong();

    //nextInt(bound) unbiased bounded integer, maps to vanilla nextInt(int)
    //Unbiased rejection sampling ensures uniform distribution; C# casts with unchecked(uint) to simulate Java toUnsignedLong
    public int NextInt(int bound)
    {
        if (bound <= 0)
            throw new ArgumentException("Bound must be positive");
        unchecked
        {
            var randomBits = (long)(uint)NextInt();
            var multipliedRandomBits = randomBits * bound;
            var fractionalPart = multipliedRandomBits & 4294967295L;
            if (fractionalPart < bound)
            {
                var unbiasedBucketsStartIndex = (int)((uint)(bound ^ -1) + 1) % (uint)bound;
                while (fractionalPart < unbiasedBucketsStartIndex)
                {
                    var randomBits2 = (long)(uint)NextInt();
                    multipliedRandomBits = randomBits2 * bound;
                    fractionalPart = multipliedRandomBits & 4294967295L;
                }
            }
            return (int)(multipliedRandomBits >> 32);
        }
    }

    public long NextLong() => _randomNumberGenerator.NextLong();

    public bool NextBoolean() => (_randomNumberGenerator.NextLong() & 1) != 0;

    public float NextFloat() => NextBits(24) * FloatUnit;

    public double NextDouble() => NextBits(53) * DoubleUnit;

    public double NextGaussian() => _gaussianSource.NextGaussian();

    //consumeCount consumes the given number of rounds, maps to vanilla consumeCount; override calls nextLong directly to avoid int truncation
    public void ConsumeCount(int rounds)
    {
        for (var i = 0; i < rounds; i++)
            _randomNumberGenerator.NextLong();
    }

    //nextBits takes the high bits, maps to vanilla nextBits; unsigned shift keeps the high bits valid
    private long NextBits(int bits)
        => _randomNumberGenerator.NextLong() >>> (64 - bits);

    //XoroshiroPositionalRandomFactory positional factory, maps to vanilla XoroshiroPositionalRandomFactory
    //Holds a two-long seed, derives a stable RandomSource from position or hash
    public sealed class XoroshiroPositionalRandomFactory : PositionalRandomFactory
    {
        private readonly long _seedLo;
        private readonly long _seedHi;

        public XoroshiroPositionalRandomFactory(long seedLo, long seedHi)
        {
            _seedLo = seedLo;
            _seedHi = seedHi;
        }

        //at derives a random source from coordinates, maps to vanilla at(int,int,int)
        //Uses Mth.getSeed to generate the position seed then XORs seedLo as the new seed
        public RandomSource At(int x, int y, int z)
        {
            var positionalSeed = Mth.GetSeed(x, y, z);
            var randomSeed = positionalSeed ^ _seedLo;
            return new XoroshiroRandomSource(randomSeed, _seedHi);
        }

        //fromHashOf derives a random source from a string hash, maps to vanilla fromHashOf(String)
        public RandomSource FromHashOf(string name)
        {
            var seed = RandomSupport.SeedFromHashOf(name);
            return new XoroshiroRandomSource(seed.Xor(_seedLo, _seedHi));
        }

        //fromSeed derives a random source from a long seed, maps to vanilla fromSeed(long)
        public RandomSource FromSeed(long seed)
            => new XoroshiroRandomSource(seed ^ _seedLo, seed ^ _seedHi);

        //parityConfigString outputs parity debug info, maps to vanilla parityConfigString
        public void ParityConfigString(StringBuilder sb)
            => sb.Append("seedLo: ").Append(_seedLo).Append(", seedHi: ").Append(_seedHi);
    }
}
