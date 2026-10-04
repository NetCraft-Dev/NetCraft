namespace NetCraft.Util.Random;

//权重随机选择静态工具对应原版net.minecraft.util.random.WeightedRandom
//提供按总权重随机选item与累加索引查找
public static class WeightedRandom
{
    //getTotalWeight累计权重对应原版getTotalWeight
    //总权重超过int.MaxValue抛ArgumentException用long累加防溢出
    public static int GetTotalWeight<T>(IReadOnlyList<T> items, Func<T, int> weightGetter)
    {
        long totalWeight = 0;
        foreach (var item in items)
            totalWeight += weightGetter(item);
        if (totalWeight > int.MaxValue)
            throw new ArgumentException("Sum of weights must be <= 2147483647");
        return (int)totalWeight;
    }

    //getRandomItem按随机数选取对应原版getRandomItem(random,items,totalWeight,weightGetter)
    //totalWeight为零返回None负数抛异常
    public static T? GetRandomItem<T>(RandomSource random, IReadOnlyList<T> items, int totalWeight, Func<T, int> weightGetter)
    {
        if (totalWeight < 0)
            throw new ArgumentException("Negative total weight in getRandomItem");
        if (totalWeight == 0)
            return default;
        var selection = random.NextInt(totalWeight);
        return GetWeightedItem(items, selection, weightGetter);
    }

    //getWeightedItem按累加索引查找对应原版getWeightedItem
    public static T? GetWeightedItem<T>(IReadOnlyList<T> items, int index, Func<T, int> weightGetter)
    {
        foreach (var item in items)
        {
            index -= weightGetter(item);
            if (index < 0)
                return item;
        }
        return default;
    }

    //getRandomItem重载内部计算总权重对应原版getRandomItem(random,items,weightGetter)
    public static T? GetRandomItem<T>(RandomSource random, IReadOnlyList<T> items, Func<T, int> weightGetter)
        => GetRandomItem(random, items, GetTotalWeight(items, weightGetter), weightGetter);
}
