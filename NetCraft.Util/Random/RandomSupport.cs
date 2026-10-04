using System.Security.Cryptography;

namespace NetCraft.Util.Random;

//随机支持工具对应原版net.minecraft.world.level.levelgen.RandomSupport
//提供种子升级128位与哈希种子生成
public static class RandomSupport
{
    //GOLDEN_RATIO_64黄金比例64位常量对应原版GOLDEN_RATIO_64
    public const long GoldenRatio64 = -7046029254386353131L;

    //SILVER_RATIO_64白银比例64位常量对应原版SILVER_RATIO_64
    public const long SilverRatio64 = 7640891576956012809L;

    private static long _seedUniquifier = 8682522807148012L;

    //mixStafford13Stafford混洗13对应原版mixStafford13
    //用于种子升级与位置哈希避免低位偏置
    public static long MixStafford13(long z)
    {
        unchecked
        {
            var z2 = (z ^ (z >>> 30)) * -4658895280553007687L;
            var z3 = (z2 ^ (z2 >>> 27)) * -7723592293110705685L;
            return z3 ^ (z3 >>> 31);
        }
    }

    //upgradeSeedTo128bitUnmixed未混洗升级到128位种子对应原版upgradeSeedTo128bitUnmixed
    public static Seed128bit UpgradeSeedTo128bitUnmixed(long legacySeed)
    {
        unchecked
        {
            var lowBits = legacySeed ^ SilverRatio64;
            var highBits = lowBits + GoldenRatio64;
            return new Seed128bit(lowBits, highBits);
        }
    }

    //upgradeSeedTo128bit升级并混洗对应原版upgradeSeedTo128bit
    public static Seed128bit UpgradeSeedTo128bit(long legacySeed)
        => UpgradeSeedTo128bitUnmixed(legacySeed).Mixed();

    //seedFromHashOf按字符串MD5哈希生成128位种子对应原版seedFromHashOf
    //C#用MD5.HashData替代Guava Hashing.md5保证字节级一致
    public static Seed128bit SeedFromHashOf(string input)
    {
        var bytes = MD5.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        var hashLo = BitConverter.ToInt64(bytes, 0);
        var hashHi = BitConverter.ToInt64(bytes, 8);
        return new Seed128bit(hashLo, hashHi);
    }

    //generateUniqueSeed生成唯一种子对应原版generateUniqueSeed
    //用Interlocked模拟AtomicLong.updateAndGet
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

    //Seed128bit 128位种子记录对应原版RandomSupport.Seed128bit
    public sealed record Seed128bit(long SeedLo, long SeedHi)
    {
        //xor与另一对long异或对应原版xor(long,long)
        public Seed128bit Xor(long lo, long hi)
            => new(SeedLo ^ lo, SeedHi ^ hi);

        //xor与另一Seed128bit异或对应原版xor(Seed128bit)
        public Seed128bit Xor(Seed128bit other) => Xor(other.SeedLo, other.SeedHi);

        //mixed对两路种子做Stafford13混洗对应原版mixed
        public Seed128bit Mixed()
            => new(MixStafford13(SeedLo), MixStafford13(SeedHi));
    }
}
