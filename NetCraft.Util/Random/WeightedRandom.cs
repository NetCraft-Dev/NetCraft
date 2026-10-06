namespace NetCraft.Util.Random;

//Weighted random selection static helpers, map to vanilla net.minecraft.util.random.WeightedRandom
//Provides random selection by total weight and cumulative index search
public static class WeightedRandom
{
    //getTotalWeight accumulates weight, maps to vanilla getTotalWeight
    //Total weight over int.MaxValue throws ArgumentException, accumulates in long to prevent overflow
    public static int GetTotalWeight<T>(IReadOnlyList<T> items, Func<T, int> weightGetter)
    {
        long totalWeight = 0;
        foreach (var item in items)
            totalWeight += weightGetter(item);
        if (totalWeight > int.MaxValue)
            throw new ArgumentException("Sum of weights must be <= 2147483647");
        return (int)totalWeight;
    }

    //getRandomItem picks at random, maps to vanilla getRandomItem(random,items,totalWeight,weightGetter)
    //Zero totalWeight returns None, negative throws
    public static T? GetRandomItem<T>(RandomSource random, IReadOnlyList<T> items, int totalWeight, Func<T, int> weightGetter)
    {
        if (totalWeight < 0)
            throw new ArgumentException("Negative total weight in getRandomItem");
        if (totalWeight == 0)
            return default;
        var selection = random.NextInt(totalWeight);
        return GetWeightedItem(items, selection, weightGetter);
    }

    //getWeightedItem does cumulative index search, maps to vanilla getWeightedItem
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

    //getRandomItem overload computes total weight internally, maps to vanilla getRandomItem(random,items,weightGetter)
    public static T? GetRandomItem<T>(RandomSource random, IReadOnlyList<T> items, Func<T, int> weightGetter)
        => GetRandomItem(random, items, GetTotalWeight(items, weightGetter), weightGetter);
}
