namespace NetCraft.Util.Random;

//WorldgenRandom 世界生成种子派生工具 对应原版 net.minecraft.world.level.levelgen.WorldgenRandom
//原版把派生逻辑做在随机源包装类上 本作直接给静态方法 底层随机源由调用方提供
//结构放置与装饰都靠这几个派生保证同种子同结果 公式与消耗顺序必须逐字对齐否则结构位置会漂
//注意结构放置要用 LegacyRandomSource 装饰用 Xoroshiro 两者数值不同不能混
public static class WorldgenRandom
{
    //SetLargeFeatureSeed 按区块坐标派生种子 对应原版 setLargeFeatureSeed
    //先取两个奇数缩放因子再异或区块坐标 结构放置与雕刻器共用同一套公式
    public static void SetLargeFeatureSeed(RandomSource random, long seed, int chunkX, int chunkZ)
    {
        random.SetSeed(seed);
        var xScale = random.NextLong() | 1L;
        var zScale = random.NextLong() | 1L;
        random.SetSeed(chunkX * xScale ^ chunkZ * zScale ^ seed);
    }

    //SetLargeFeatureWithSalt 带盐派生种子 对应原版 setLargeFeatureWithSalt
    //随机散布放置用它算某个网格的潜在区块 是纯线性组合不消耗随机数
    public static void SetLargeFeatureWithSalt(RandomSource random, long seed, int x, int z, int salt)
    {
        random.SetSeed(x * 341873128712L + z * 132897987541L + seed + salt);
    }

    //SetDecorationSeed 派生装饰种子并返回 对应原版 setDecorationSeed
    //装饰与结构落地都以它为基准 返回值要接着喂给 SetFeatureSeed
    public static long SetDecorationSeed(RandomSource random, long worldSeed, int minBlockX, int minBlockZ)
    {
        random.SetSeed(worldSeed);
        var xScale = random.NextLong() | 1L;
        var zScale = random.NextLong() | 1L;
        var seed = minBlockX * xScale ^ minBlockZ * zScale ^ worldSeed;
        random.SetSeed(seed);
        return seed;
    }

    //SetFeatureSeed 按步内序号派生种子 对应原版 setFeatureSeed
    //步长取 10000 保证同一步内相邻序号互不重叠 特征与结构都用它
    public static void SetFeatureSeed(RandomSource random, long decorationSeed, int index, int step)
        => random.SetSeed(decorationSeed + index + 10000L * step);
}
