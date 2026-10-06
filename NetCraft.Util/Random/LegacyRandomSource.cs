using System.Text;

namespace NetCraft.Util.Random;

//LegacyRandomSource legacy linear congruential random source, maps to vanilla net.minecraft.world.level.levelgen.LegacyRandomSource
//48-bit LCG compatible with Java Random's historical seed sequence; EndIslandDensityFunction relies on this source to generate End island noise
//MULTIPLIER/INCREMENT/MODULUS_MASK align with java.util.Random constants to match vanilla End generation
public sealed class LegacyRandomSource : RandomSource
{
    private const int ModulusBits = 48;
    private const long ModulusMask = 281474976710655L;
    private const long Multiplier = 25214903917L;
    private const long Increment = 11;

    //FLOAT_UNIT 24-bit float unit 2^-24, aligns with vanilla FLOAT_UNIT
    private const float FloatUnit = 5.9604645E-8f;

    //DOUBLE_UNIT 53-bit double unit 2^-53, aligns with vanilla DOUBLE_UNIT
    private const double DoubleUnit = 1.1102230246251565E-16d;

    private long _seed;
    private readonly MarsagliaPolarGaussian _gaussianSource;

    public LegacyRandomSource(long seed)
    {
        _seed = (seed ^ Multiplier) & ModulusMask;
        _gaussianSource = new MarsagliaPolarGaussian(this);
    }

    public RandomSource Fork() => new LegacyRandomSource(NextLong());

    public PositionalRandomFactory ForkPositional() => new LegacyPositionalRandomFactory(NextLong());

    public void SetSeed(long seed)
    {
        _seed = (seed ^ Multiplier) & ModulusMask;
        _gaussianSource.Reset();
    }

    //Next core LCG advance, maps to vanilla next(bits)
    //After advancing the seed, takes the high bits; returns bits<=32
    private int Next(int bits)
    {
        _seed = (_seed * Multiplier + Increment) & ModulusMask;
        return (int)(_seed >>> (ModulusBits - bits));
    }

    public int NextInt() => Next(32);

    //nextInt(bound) unbiased bounded integer, maps to vanilla java.util.Random.nextInt(int)
    //For powers of two shifts directly, otherwise rejection sampling ensures uniform distribution
    public int NextInt(int bound)
    {
        if (bound <= 0)
            throw new ArgumentException("Bound must be positive");
        if ((bound & (bound - 1)) == 0)
            return (int)((bound * (long)Next(31)) >> 31);
        int bits, val;
        do
        {
            bits = Next(31);
            val = bits % bound;
        } while (bits - val + (bound - 1) < 0);
        return val;
    }

    public long NextLong() => ((long)Next(32) << 32) + Next(32);

    public bool NextBoolean() => Next(1) != 0;

    public float NextFloat() => Next(24) * FloatUnit;

    public double NextDouble() => (((long)Next(26) << 27) + Next(27)) * DoubleUnit;

    public double NextGaussian() => _gaussianSource.NextGaussian();

    //consumeCount override goes through next to avoid nextInt truncation, aligning with vanilla BitRandomSource default behavior
    public void ConsumeCount(int rounds)
    {
        for (var i = 0; i < rounds; i++)
            Next(32);
    }

    //LegacyPositionalRandomFactory legacy positional factory, maps to vanilla LegacyPositionalRandomFactory
    //Derives a stable RandomSource by XORing the seed with coordinates or a hash
    public sealed class LegacyPositionalRandomFactory : PositionalRandomFactory
    {
        private readonly long _seed;

        public LegacyPositionalRandomFactory(long seed) { _seed = seed; }

        public RandomSource At(int x, int y, int z)
        {
            var positionalSeed = Mth.GetSeed(x, y, z);
            return new LegacyRandomSource(positionalSeed ^ _seed);
        }

        //FromHashOf derives a random source from a string hash, maps to vanilla fromHashOf(String)
        //Vanilla uses Java String.hashCode and must match bit for bit: C#'s GetHashCode is randomized per process by default, so using it as a seed would make terrain drift across processes
        public RandomSource FromHashOf(string name)
        {
            var positionalSeed = JavaStringHash(name);
            return new LegacyRandomSource(positionalSeed ^ _seed);
        }

        //JavaStringHash reproduces java.lang.String.hashCode's base-31 rolling hash
        private static int JavaStringHash(string value)
        {
            var hash = 0;
            foreach (var c in value)
                hash = unchecked(hash * 31 + c);
            return hash;
        }

        public RandomSource FromSeed(long seed) => new LegacyRandomSource(seed);

        public void ParityConfigString(StringBuilder sb)
            => sb.Append("LegacyPositionalRandomFactory{").Append(_seed).Append('}');
    }
}
