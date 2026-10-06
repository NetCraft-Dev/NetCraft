using NetCraft.Util.Random;

namespace NetCraft.Util.Collection;

//Random collection helpers, map to the random-related collection methods in vanilla net.minecraft.util.Util
//Ports GetRandom/GetRandomSafe/ToShuffledList/ShuffledCopy/Shuffle
public static class RandomCollections
{
    //getRandom picks an array element at random, maps to vanilla Util.getRandom(T[])
    //Empty array throws IndexOutOfRange, aligning with vanilla ArrayIndexOutOfBounds
    public static T GetRandom<T>(IReadOnlyList<T> array, RandomSource random)
        => array[random.NextInt(array.Count)];

    //getRandom picks an int array element at random, maps to vanilla Util.getRandom(int[])
    public static int GetRandom(int[] array, RandomSource random)
        => array[random.NextInt(array.Length)];

    //getRandom picks a list element at random, maps to vanilla Util.getRandom(List)
    public static T GetRandom<T>(T[] array, RandomSource random)
        => array[random.NextInt(array.Length)];

    //getRandomSafe returns None for an empty list, otherwise Some, maps to vanilla Util.getRandomSafe
    public static Option<T> GetRandomSafe<T>(IReadOnlyList<T> list, RandomSource random)
        => list.Count == 0 ? Option<T>.None() : Option<T>.Some(GetRandom(list, random));

    //shuffle in-place shuffle, maps to vanilla Util.shuffle
    //Fisher-Yates reverse traversal swapping i-1 with swapTo
    public static void Shuffle<T>(IList<T> list, RandomSource random)
    {
        var size = list.Count;
        for (var i = size; i > 1; i--)
        {
            var swapTo = random.NextInt(i);
            (list[i - 1], list[swapTo]) = (list[swapTo], list[i - 1]);
        }
    }

    //toShuffledList collects a stream into a list then shuffles, maps to vanilla Util.toShuffledList(Stream)
    public static List<T> ToShuffledList<T>(IEnumerable<T> source, RandomSource random)
    {
        var result = source.ToList();
        Shuffle(result, random);
        return result;
    }

    //toShuffledList collects an int stream into an array then shuffles, maps to vanilla Util.toShuffledList(IntStream)
    //Renamed ToShuffledIntArray to avoid overload conflict with the generic ToShuffledList<int>
    public static int[] ToShuffledIntArray(IEnumerable<int> source, RandomSource random)
    {
        var result = source.ToArray();
        var size = result.Length;
        for (var i = size; i > 1; i--)
        {
            var swapTo = random.NextInt(i);
            (result[i - 1], result[swapTo]) = (result[swapTo], result[i - 1]);
        }
        return result;
    }

    //shuffledCopy copies an array then shuffles, maps to vanilla Util.shuffledCopy(T[])
    public static List<T> ShuffledCopy<T>(T[] array, RandomSource random)
    {
        var copy = new List<T>(array);
        Shuffle(copy, random);
        return copy;
    }

    //shuffledCopy copies a list then shuffles, maps to vanilla Util.shuffledCopy(ObjectArrayList)
    public static List<T> ShuffledCopy<T>(IReadOnlyList<T> list, RandomSource random)
    {
        var copy = new List<T>(list);
        Shuffle(copy, random);
        return copy;
    }
}
