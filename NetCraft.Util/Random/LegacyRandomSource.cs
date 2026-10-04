using System.Text;

namespace NetCraft.Util.Random;

//LegacyRandomSource 旧版线性同余随机源对应原版 net.minecraft.world.level.levelgen.LegacyRandomSource
//48 位 LCG 算法兼容 Java Random 历史种子序列EndIslandDensityFunction 依赖此源生成末地岛屿噪声
//MULTIPLIER/INCREMENT/MODULUS_MASK 对齐 java.util.Random 常量保证与原版末地生成一致
public sealed class LegacyRandomSource : RandomSource
{
    private const int ModulusBits = 48;
    private const long ModulusMask = 281474976710655L;
    private const long Multiplier = 25214903917L;
    private const long Increment = 11;

    //FLOAT_UNIT 24 位浮点单位 2^-24 对齐原版 FLOAT_UNIT
    private const float FloatUnit = 5.9604645E-8f;

    //DOUBLE_UNIT 53 位双精度单位 2^-53 对齐原版 DOUBLE_UNIT
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

    //Next 核心 LCG 推进对应原版 next(bits)
    //推进种子后取高 bits 位返回 bits<=32
    private int Next(int bits)
    {
        _seed = (_seed * Multiplier + Increment) & ModulusMask;
        return (int)(_seed >>> (ModulusBits - bits));
    }

    public int NextInt() => Next(32);

    //nextInt(bound) 无偏有界整数对应原版 java.util.Random.nextInt(int)
    //2 的幂次直接位移否则拒绝采样保证均匀分布
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

    //consumeCount 重写走 next 避免nextInt 截断对齐原版 BitRandomSource 默认行为
    public void ConsumeCount(int rounds)
    {
        for (var i = 0; i < rounds; i++)
            Next(32);
    }

    //LegacyPositionalRandomFactory 旧版位置性工厂对应原版 LegacyPositionalRandomFactory
    //按坐标或哈希异或种子派生稳定 RandomSource
    public sealed class LegacyPositionalRandomFactory : PositionalRandomFactory
    {
        private readonly long _seed;

        public LegacyPositionalRandomFactory(long seed) { _seed = seed; }

        public RandomSource At(int x, int y, int z)
        {
            var positionalSeed = Mth.GetSeed(x, y, z);
            return new LegacyRandomSource(positionalSeed ^ _seed);
        }

        //FromHashOf 按字符串哈希派生随机源对应原版 fromHashOf(String)
        //原版取 Java String.hashCode 必须逐位对齐: C# 的 GetHashCode 默认每进程随机化 用它做种子会让地形跨进程漂移
        public RandomSource FromHashOf(string name)
        {
            var positionalSeed = JavaStringHash(name);
            return new LegacyRandomSource(positionalSeed ^ _seed);
        }

        //JavaStringHash 复刻 java.lang.String.hashCode 的 31 进制滚动哈希
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
