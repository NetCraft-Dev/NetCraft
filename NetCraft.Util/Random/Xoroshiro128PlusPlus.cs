using System.Numerics;

namespace NetCraft.Util.Random;

//Xoroshiro128++ core random number generator, maps to vanilla net.minecraft.world.level.levelgen.Xoroshiro128PlusPlus
//Dual 64-bit state seedLo/seedHi, bit ops fully preserved to keep cross-language sequences consistent
public sealed class Xoroshiro128PlusPlus
{
    private long _seedLo;
    private long _seedHi;

    //Constructs from Seed128bit, maps to vanilla Xoroshiro128PlusPlus(Seed128bit)
    public Xoroshiro128PlusPlus(RandomSupport.Seed128bit seed)
        : this(seed.SeedLo, seed.SeedHi) { }

    //Constructs from two longs, maps to vanilla Xoroshiro128PlusPlus(long,long)
    //An all-zero state breaks the generator, so it is replaced with the golden/silver ratios to avoid degeneracy
    public Xoroshiro128PlusPlus(long seedLo, long seedHi)
    {
        _seedLo = seedLo;
        _seedHi = seedHi;
        if ((_seedLo | _seedHi) == 0)
        {
            _seedLo = RandomSupport.GoldenRatio64;
            _seedHi = RandomSupport.SilverRatio64;
        }
    }

    //nextLong generates the next 64-bit value, maps to vanilla nextLong
    //Bit ops include rotateLeft and XOR-shift; C# uses unchecked so long overflow wraps like Java
    //BitOperations.RotateLeft with an explicit ulong cast aligns with Java Long.rotateLeft unsigned rotation
    public long NextLong()
    {
        unchecked
        {
            var s0 = _seedLo;
            var s1 = _seedHi;
            var result = (long)BitOperations.RotateLeft((ulong)(s0 + s1), 17) + s0;
            var s12 = s1 ^ s0;
            _seedLo = (long)BitOperations.RotateLeft((ulong)s0, 49) ^ s12 ^ (s12 << 21);
            _seedHi = (long)BitOperations.RotateLeft((ulong)s12, 28);
            return result;
        }
    }
}
