using System.Text;

namespace NetCraft.Util.Random;

//Xoroshiro随机源主实现对应原版net.minecraft.world.level.levelgen.XoroshiroRandomSource
//包装Xoroshiro128PlusPlus提供RandomSource接口与位置性工厂
public sealed class XoroshiroRandomSource : RandomSource
{
    //FLOAT_UNIT 24位浮点单位2^-24对应原版FLOAT_UNIT
    private const float FloatUnit = 5.9604645E-8f;

    //DOUBLE_UNIT 53位双精度单位2^-53对应原版DOUBLE_UNIT
    private const double DoubleUnit = 1.1102230246251565E-16d;

    private Xoroshiro128PlusPlus _randomNumberGenerator;
    private readonly MarsagliaPolarGaussian _gaussianSource;

    //按单long种子构造对应原版XoroshiroRandomSource(long)
    //单long种子升级到128位保证状态空间
    public XoroshiroRandomSource(long seed)
    {
        _randomNumberGenerator = new Xoroshiro128PlusPlus(RandomSupport.UpgradeSeedTo128bit(seed));
        _gaussianSource = new MarsagliaPolarGaussian(this);
    }

    //按Seed128bit构造对应原版XoroshiroRandomSource(Seed128bit)
    public XoroshiroRandomSource(RandomSupport.Seed128bit seed)
    {
        _randomNumberGenerator = new Xoroshiro128PlusPlus(seed);
        _gaussianSource = new MarsagliaPolarGaussian(this);
    }

    //按双long构造对应原版XoroshiroRandomSource(long,long)
    public XoroshiroRandomSource(long seedLo, long seedHi)
    {
        _randomNumberGenerator = new Xoroshiro128PlusPlus(seedLo, seedHi);
        _gaussianSource = new MarsagliaPolarGaussian(this);
    }

    //fork派生新随机源对应原版fork
    //用当前生成器两次nextLong作为新种子避免相关性
    public RandomSource Fork()
        => new XoroshiroRandomSource(_randomNumberGenerator.NextLong(), _randomNumberGenerator.NextLong());

    //forkPositional派生位置性工厂对应原版forkPositional
    public PositionalRandomFactory ForkPositional()
        => new XoroshiroPositionalRandomFactory(_randomNumberGenerator.NextLong(), _randomNumberGenerator.NextLong());

    //setSeed重置种子并清空高斯缓存对应原版setSeed
    public void SetSeed(long seed)
    {
        _randomNumberGenerator = new Xoroshiro128PlusPlus(RandomSupport.UpgradeSeedTo128bit(seed));
        _gaussianSource.Reset();
    }

    public int NextInt() => (int)_randomNumberGenerator.NextLong();

    //nextInt(bound)无偏有界整数对应原版nextInt(int)
    //无偏拒绝采样保证均匀分布C#用unchecked(uint)强转模拟Java toUnsignedLong
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

    //consumeCount消耗指定轮次对应原版consumeCount重写直接调nextLong避免int截断
    public void ConsumeCount(int rounds)
    {
        for (var i = 0; i < rounds; i++)
            _randomNumberGenerator.NextLong();
    }

    //nextBits取高bits位对应原版nextBits无符号右移保证高位有效
    private long NextBits(int bits)
        => _randomNumberGenerator.NextLong() >>> (64 - bits);

    //XoroshiroPositionalRandomFactory位置性工厂对应原版XoroshiroPositionalRandomFactory
    //持有双long种子按位置或哈希派生稳定RandomSource
    public sealed class XoroshiroPositionalRandomFactory : PositionalRandomFactory
    {
        private readonly long _seedLo;
        private readonly long _seedHi;

        public XoroshiroPositionalRandomFactory(long seedLo, long seedHi)
        {
            _seedLo = seedLo;
            _seedHi = seedHi;
        }

        //at按坐标派生随机源对应原版at(int,int,int)
        //用Mth.getSeed生成位置种子后异或seedLo作为新种子
        public RandomSource At(int x, int y, int z)
        {
            var positionalSeed = Mth.GetSeed(x, y, z);
            var randomSeed = positionalSeed ^ _seedLo;
            return new XoroshiroRandomSource(randomSeed, _seedHi);
        }

        //fromHashOf按字符串哈希派生随机源对应原版fromHashOf(String)
        public RandomSource FromHashOf(string name)
        {
            var seed = RandomSupport.SeedFromHashOf(name);
            return new XoroshiroRandomSource(seed.Xor(_seedLo, _seedHi));
        }

        //fromSeed按long种子派生随机源对应原版fromSeed(long)
        public RandomSource FromSeed(long seed)
            => new XoroshiroRandomSource(seed ^ _seedLo, seed ^ _seedHi);

        //parityConfigString输出奇偶校验调试信息对应原版parityConfigString
        public void ParityConfigString(StringBuilder sb)
            => sb.Append("seedLo: ").Append(_seedLo).Append(", seedHi: ").Append(_seedHi);
    }
}
