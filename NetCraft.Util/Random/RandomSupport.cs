using System.Security.Cryptography;

namespace NetCraft.Util.Random;

//Random support helpers, map to vanilla net.minecraft.world.level.levelgen.RandomSupport
//Provides 128-bit seed upgrade and hash seed generation
public static class RandomSupport
{
    //GOLDEN_RATIO_64 golden ratio 64-bit constant, maps to vanilla GOLDEN_RATIO_64
    public const long GoldenRatio64 = -7046029254386353131L;

    //SILVER_RATIO_64 silver ratio 64-bit constant, maps to vanilla SILVER_RATIO_64
    public const long SilverRatio64 = 7640891576956012809L;

    private static long _seedUniquifier = 8682522807148012L;

    //mixStafford13 Stafford mix13, maps to vanilla mixStafford13
    //Used for seed upgrade and positional hashing to avoid low-bit bias
    public static long MixStafford13(long z)
    {
        unchecked
        {
            var z2 = (z ^ (z >>> 30)) * -4658895280553007687L;
            var z3 = (z2 ^ (z2 >>> 27)) * -7723592293110705685L;
            return z3 ^ (z3 >>> 31);
        }
    }

    //upgradeSeedTo128bitUnmixed unmixed upgrade to a 128-bit seed, maps to vanilla upgradeSeedTo128bitUnmixed
    public static Seed128bit UpgradeSeedTo128bitUnmixed(long legacySeed)
    {
        unchecked
        {
            var lowBits = legacySeed ^ SilverRatio64;
            var highBits = lowBits + GoldenRatio64;
            return new Seed128bit(lowBits, highBits);
        }
    }

    //upgradeSeedTo128bit upgrades and mixes, maps to vanilla upgradeSeedTo128bit
    public static Seed128bit UpgradeSeedTo128bit(long legacySeed)
        => UpgradeSeedTo128bitUnmixed(legacySeed).Mixed();

    //seedFromHashOf generates a 128-bit seed from a string MD5 hash, maps to vanilla seedFromHashOf
    //C# uses MD5.HashData instead of Guava Hashing.md5 to guarantee byte-level consistency
    public static Seed128bit SeedFromHashOf(string input)
    {
        var bytes = MD5.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        var hashLo = BitConverter.ToInt64(bytes, 0);
        var hashHi = BitConverter.ToInt64(bytes, 8);
        return new Seed128bit(hashLo, hashHi);
    }

    //generateUniqueSeed generates a unique seed, maps to vanilla generateUniqueSeed
    //Uses Interlocked to simulate AtomicLong.updateAndGet
    public static long GenerateUniqueSeed()
    {
        long current, newValue;
        do
        {
            current = Interlocked.Read(ref _seedUniquifier);
            unchecked
            {
                newValue = current * 1181783497276652981L;
            }
        } while (Interlocked.CompareExchange(ref _seedUniquifier, newValue, current) != current);
        return unchecked(newValue ^ DateTimeOffset.UtcNow.Ticks);
    }

    //Seed128bit 128-bit seed record, maps to vanilla RandomSupport.Seed128bit
    public sealed record Seed128bit(long SeedLo, long SeedHi)
    {
        //xor XORs with another long pair, maps to vanilla xor(long,long)
        public Seed128bit Xor(long lo, long hi)
            => new(SeedLo ^ lo, SeedHi ^ hi);

        //xor XORs with another Seed128bit, maps to vanilla xor(Seed128bit)
        public Seed128bit Xor(Seed128bit other) => Xor(other.SeedLo, other.SeedHi);

        //mixed applies Stafford13 mixing to both seed parts, maps to vanilla mixed
        public Seed128bit Mixed()
            => new(MixStafford13(SeedLo), MixStafford13(SeedHi));
    }
}
