namespace NetCraft.Util.Random;

//随机源接口对应原版net.minecraft.util.RandomSource
//所有随机数生成入口定义fork与各类next方法
public interface RandomSource
{
    //fork派生新独立随机源对应原版fork
    RandomSource Fork();

    //forkPositional派生位置性工厂对应原版forkPositional
    PositionalRandomFactory ForkPositional();

    //setSeed重置种子对应原版setSeed
    void SetSeed(long seed);

    int NextInt();
    int NextInt(int bound);
    long NextLong();
    bool NextBoolean();
    float NextFloat();
    double NextDouble();
    double NextGaussian();

    //consumeCount消耗指定轮次对应原版consumeCount默认实现调nextInt
    void ConsumeCount(int rounds)
    {
        for (var i = 0; i < rounds; i++)
            NextInt();
    }

    //nextIntBetweenInclusive闭区间随机整数对应原版nextIntBetweenInclusive
    int NextIntBetweenInclusive(int min, int maxInclusive)
        => NextInt(maxInclusive - min + 1) + min;

    //triangle三角分布对应原版triangle(double)
    double Triangle(double mean, double spread)
        => mean + spread * (NextDouble() - NextDouble());

    //triangle三角分布float重载
    float Triangle(float mean, float spread)
        => mean + spread * (NextFloat() - NextFloat());

    //nextInt带origin重载对应原版nextInt(origin,bound)
    int NextInt(int origin, int bound)
    {
        if (origin >= bound)
            throw new ArgumentException("bound - origin is non positive");
        return origin + NextInt(bound - origin);
    }

    //create默认工厂对应原版create生成唯一种子
    static RandomSource Create() => Create(RandomSupport.GenerateUniqueSeed());

    //create按种子构造LegacyRandomSource占位用XoroshiroRandomSource等Legacy阶段补
    static RandomSource Create(long seed) => new XoroshiroRandomSource(seed);
}
